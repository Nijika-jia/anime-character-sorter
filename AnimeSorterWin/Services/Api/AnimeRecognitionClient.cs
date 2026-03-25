using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeSorterWin.Models;

namespace AnimeSorterWin.Services.Api;

/// <summary>
/// 调用外部识别 API，并内置 429 的全局退避/重试。
/// </summary>
public sealed class AnimeRecognitionClient
{
    private static readonly int[] SuccessCodes = [0, 200, 17720, 17721];

    private readonly HttpClient _httpClient;
    private readonly ThrottledHttpExecutor _executor;
    private readonly AnimeApiSettings _settings;

    public AnimeRecognitionClient(HttpClient httpClient, ThrottledHttpExecutor executor, AnimeApiSettings settings)
    {
        _httpClient = httpClient;
        _executor = executor;
        _settings = settings;
    }

    /// <summary>
    /// 识别一张图片。若失败或结果为空，则返回 Unknown。
    /// </summary>
    public async Task<(RecognitionResult Result, int Throttled429Count)> RecognizeAsync(string imagePath, CancellationToken ct)
    {
        var (candidatesData, throttled429Count) = await RecognizeCandidatesAsync(imagePath, ct).ConfigureAwait(false);
        var first = candidatesData.Characters.FirstOrDefault();
        var series = first?.Work ?? "Unknown";
        var character = first?.Character ?? "Unknown";
        if (string.IsNullOrWhiteSpace(series) || string.IsNullOrWhiteSpace(character))
            return (ToUnknownResult(), throttled429Count);

        return (new RecognitionResult(series, character, candidatesData.NotConfident ? "Unknown" : "Success"), throttled429Count);
    }

    /// <summary>
    /// 识别一张图片并返回“完整候选集”：所有 work/character + box + not_confident。
    /// </summary>
    public async Task<(RecognitionCandidatesData Data, int Throttled429Count)> RecognizeCandidatesAsync(string imagePath, CancellationToken ct)
    {
        var backoffDelay = _settings.BackoffBaseDelay;
        var throttled429Count = 0;

        for (int attempt = 0; attempt <= _settings.MaxRetriesOn429; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            await _executor.WaitForTurnAsync(ct).ConfigureAwait(false);

            try
            {
                using var requestContent = await BuildRequestContentAsync(imagePath, ct).ConfigureAwait(false);

                using var resp = await _httpClient.PostAsync(_settings.Url, requestContent, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throttled429Count++;
                    _executor.TriggerGlobalPause(backoffDelay);

                    if (attempt >= _settings.MaxRetriesOn429)
                        return (ToUnknownCandidates(), throttled429Count);

                    await Task.Delay(backoffDelay, ct).ConfigureAwait(false);
                    backoffDelay = TimeSpan.FromTicks(Math.Min(backoffDelay.Ticks * 2, _settings.BackoffMaxDelay.Ticks));
                    continue;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    if (attempt >= _settings.MaxRetriesOn429)
                        return (ToUnknownCandidates(), throttled429Count);

                    // 把“退避等待”也作为全局暂停暴露给 UI（便于看到剩余时间）
                    _executor.TriggerGlobalPause(backoffDelay);
                    await Task.Delay(backoffDelay, ct).ConfigureAwait(false);
                    backoffDelay = TimeSpan.FromTicks(Math.Min(backoffDelay.Ticks * 2, _settings.BackoffMaxDelay.Ticks));
                    continue;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse>(stream, cancellationToken: ct).ConfigureAwait(false);
                if (apiResponse is null)
                    return (ToUnknownCandidates(), throttled429Count);

                if (IsRetryableCode(apiResponse.Code))
                {
                    if (attempt >= _settings.MaxRetriesOn429)
                        return (ToUnknownCandidates(), throttled429Count);

                    if (IsThrottleLikeCode(apiResponse.Code))
                    {
                        // 17728/17731 这类“像限流一样”的返回码也会导致长时间等待，
                        // 如果不计入统计，UI 看起来会像“卡住”。
                        throttled429Count++;
                        _executor.TriggerGlobalPause(backoffDelay);
                    }

                    await Task.Delay(backoffDelay, ct).ConfigureAwait(false);
                    backoffDelay = TimeSpan.FromTicks(Math.Min(backoffDelay.Ticks * 2, _settings.BackoffMaxDelay.Ticks));
                    continue;
                }

                if (!IsSuccessCode(apiResponse.Code))
                    return (ToUnknownCandidates(), throttled429Count);

                return (ParseCandidates(apiResponse), throttled429Count);
            }
            catch when (attempt < _settings.MaxRetriesOn429)
            {
                // 网络异常/解析异常：同样做退避，并把剩余时间暴露给 UI
                _executor.TriggerGlobalPause(backoffDelay);
                await Task.Delay(backoffDelay, ct).ConfigureAwait(false);
                backoffDelay = TimeSpan.FromTicks(Math.Min(backoffDelay.Ticks * 2, _settings.BackoffMaxDelay.Ticks));
            }
            catch
            {
                return (ToUnknownCandidates(), throttled429Count);
            }
            finally
            {
                _executor.ReleaseAfterRequest();
            }
        }

        return (ToUnknownCandidates(), throttled429Count);
    }

    private async Task<HttpContent> BuildRequestContentAsync(string imagePath, CancellationToken ct)
    {
        // 按文档：参数支持 file/base64 三选一，其他可选参数作为表单字段一起提交。
        var multipart = new MultipartFormDataContent();

        // 必选之一：file 或 base64
        if (_settings.RequestMode == ApiRequestMode.Base64)
        {
            // Base64 模式会把整张图读入内存：仅用于兼容。
            var bytes = await File.ReadAllBytesAsync(imagePath, ct).ConfigureAwait(false);
            var base64 = Convert.ToBase64String(bytes);
            multipart.Add(new StringContent(base64), _settings.Base64FormFieldName);
        }
        else
        {
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };

            var fs = new FileStream(
                imagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var streamContent = new StreamContent(fs);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            multipart.Add(streamContent, _settings.ImageFormFieldName, Path.GetFileName(imagePath));
        }

        // 可选字段：is_multi/model/ai_detect（按文档必填字段名）
        multipart.Add(new StringContent(_settings.IsMulti.ToString(System.Globalization.CultureInfo.InvariantCulture)), "is_multi");
        multipart.Add(new StringContent(_settings.Model), "model");
        multipart.Add(new StringContent(_settings.AiDetect.ToString(System.Globalization.CultureInfo.InvariantCulture)), "ai_detect");

        return multipart;
    }

    private static RecognitionResult ToUnknownResult() => new("Unknown", "Unknown", "Unknown");

    private static RecognitionCandidatesData ToUnknownCandidates()
    {
        return new RecognitionCandidatesData(
            Box: [0, 0, 0, 0],
            BoxId: string.Empty,
            NotConfident: true,
            Characters: [new CandidateWorkCharacter("Unknown", "Unknown")]);
    }

    private RecognitionCandidatesData ParseCandidates(ApiResponse apiResponse)
    {
        var item = apiResponse.Data?.FirstOrDefault();
        var box = item?.Box?.ToArray() ?? new double[] { 0, 0, 0, 0 };
        if (box.Length != 4)
            box = new double[] { 0, 0, 0, 0 };

        var boxId = item?.BoxId ?? string.Empty;
        var notConfident = item?.NotConfident ?? true;

        var characters = item?.Character?
            .Select(c => new CandidateWorkCharacter(
                (c.Work ?? string.Empty).Trim(),
                (c.CharacterName ?? string.Empty).Trim()))
            .Where(c => !string.IsNullOrWhiteSpace(c.Work) && !string.IsNullOrWhiteSpace(c.Character))
            .ToArray() ?? Array.Empty<CandidateWorkCharacter>();

        if (characters.Length == 0)
            characters = [new CandidateWorkCharacter("Unknown", "Unknown")];

        return new RecognitionCandidatesData(box, boxId, notConfident, characters);
    }

    private static bool IsSuccessCode(int code) => SuccessCodes.Contains(code);

    private static bool IsThrottleLikeCode(int code) =>
        code == 17728 // 已达到本次使用上限
        || code == 17731; // 服务利用人数过多

    private static bool IsRetryableCode(int code)
    {
        // 这些错误通常是“暂时性”：服务器繁忙/内部错误/并发过高/触发使用上限等
        return code == 17702 // 503 服务器繁忙，请重试
               || code == 17706 // 识别无法完成（内部错误，请重试）
               || code == 17707 // 内部错误
               || code == 17722 // 图片下载失败（可能是瞬时）
               || code == 17728 // 使用上限（触发后需退避）
               || code == 17731; // 服务利用人数过多
    }

    private sealed class ApiResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<ApiData>? Data { get; set; }
    }

    private sealed class ApiData
    {
        [JsonPropertyName("box")]
        public List<double>? Box { get; set; }

        [JsonPropertyName("box_id")]
        public string? BoxId { get; set; }

        [JsonPropertyName("not_confident")]
        public bool? NotConfident { get; set; }

        [JsonPropertyName("character")]
        public List<ApiCharacter>? Character { get; set; }
    }

    private sealed class ApiCharacter
    {
        [JsonPropertyName("work")]
        public string? Work { get; set; }

        // JSON 字段名为 "character"，但为了避免和外层概念冲突，这里命名为 CharacterName。
        [JsonPropertyName("character")]
        public string? CharacterName { get; set; }
    }
}

