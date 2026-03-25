using System.Threading.Tasks.Dataflow;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AnimeSorterWin.Data;
using AnimeSorterWin.Data.Entities;
using AnimeSorterWin.Models;
using AnimeSorterWin.Services.Api;
using AnimeSorterWin.Utilities;
using AnimeSorterWin.Utilities.Hashing;
using Microsoft.EntityFrameworkCore;


namespace AnimeSorterWin.Services;

public sealed class ProcessingPipelineService
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnimeApiSettings _baseApiSettings;

    public ProcessingPipelineService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        AnimeApiSettings baseApiSettings)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _baseApiSettings = baseApiSettings;
    }

    public async Task RunAsync(
        PipelineRequest request,
        IProgress<PipelineProgressUpdate> progress,
        CancellationToken ct)
    {
        var stats = new ProcessingStats();

        // 每次运行根据 UI 参数构建“全局节流器 + API client”：
        // - 并发限制由 ApiMaxDegreeOfParallelism 决定
        // - 每秒请求数由 ApiMaxRps 决定
        var executor = new ThrottledHttpExecutor(request.ApiMaxDegreeOfParallelism, request.ApiMaxRps);
        // 避免修改单例配置对象：每次运行拷贝一份并覆盖请求模式。
        var settings = new AnimeApiSettings
        {
            Url = _baseApiSettings.Url,
            ImageFormFieldName = _baseApiSettings.ImageFormFieldName,
            Base64FormFieldName = _baseApiSettings.Base64FormFieldName,
            IsMulti = _baseApiSettings.IsMulti,
            Model = _baseApiSettings.Model,
            AiDetect = _baseApiSettings.AiDetect,
            RequestMode = request.ApiRequestMode,
            BackoffBaseDelay = _baseApiSettings.BackoffBaseDelay,
            BackoffMaxDelay = _baseApiSettings.BackoffMaxDelay,
            MaxRetriesOn429 = _baseApiSettings.MaxRetriesOn429,
            DefaultMaxConcurrentRequests = request.ApiMaxDegreeOfParallelism,
            DefaultMaxRps = request.ApiMaxRps
        };
        var httpClient = _httpClientFactory.CreateClient("anime_api");
        var apiClient = new AnimeRecognitionClient(httpClient, executor, settings);

        var fileBuffer = new BufferBlock<string>(new DataflowBlockOptions
        {
            BoundedCapacity = 2000,
            CancellationToken = ct
        });

        var hashBlock = new TransformBlock<string, PipelineItem>(async filePath =>
        {
            stats.IncrementScanned();
            progress.Report(ToProgressUpdate(stats, executor));

            var md5 = await Md5Hasher.ComputeMd5HexAsync(filePath, ct).ConfigureAwait(false);

            // SQLite 查缓存：避免二次识别。
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var cached = await db.RecognitionCaches.FirstOrDefaultAsync(x => x.Md5 == md5, ct).ConfigureAwait(false);

            if (cached is not null && string.Equals(cached.ResultStatus, "Success", StringComparison.OrdinalIgnoreCase))
            {
                stats.IncrementCacheHit();
                progress.Report(ToProgressUpdate(stats, executor));

                return new PipelineItem(
                    SourcePath: filePath,
                    Md5: md5,
                    Result: new RecognitionResult(cached.Series, cached.Character, cached.ResultStatus ?? "Unknown"),
                    NeedsApi: false);
            }

            return new PipelineItem(
                SourcePath: filePath,
                Md5: md5,
                Result: new RecognitionResult("Unknown", "Unknown", "Unknown"),
                NeedsApi: true);
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            BoundedCapacity = 1000,
            CancellationToken = ct
        });

        // Scanner -> Hash
        fileBuffer.LinkTo(hashBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // API：仅对 NeedsApi 的项执行
        var apiBlock = new TransformBlock<PipelineItem, PipelineItem>(async item =>
        {
            var (result, throttled429Count) = await apiClient.RecognizeAsync(item.SourcePath, ct).ConfigureAwait(false);
            if (throttled429Count > 0)
                stats.AddThrottled429(throttled429Count);

            if (result.Status == "Success")
            {
                stats.IncrementApiSuccess();
                progress.Report(ToProgressUpdate(stats, executor));
            }
            else
            {
                stats.IncrementApiUnknown();
                stats.IncrementApiFailures();
                progress.Report(ToProgressUpdate(stats, executor));
            }

            // 写入缓存：不做任何“相似度阈值”，只要 API 返回有效名称就落库。
            await using (var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
            {
                // 使用 Upsert 方式更新缓存，避免并发 Add 主键冲突。
                var existing = await db.RecognitionCaches.FirstOrDefaultAsync(x => x.Md5 == item.Md5, ct).ConfigureAwait(false);
                if (existing is null)
                {
                    db.RecognitionCaches.Add(new RecognitionCacheEntity
                    {
                        Md5 = item.Md5,
                        Series = result.Series,
                        Character = result.Character,
                        ResultStatus = result.Status,
                        RecognizedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Series = result.Series;
                    existing.Character = result.Character;
                    existing.ResultStatus = result.Status;
                    existing.RecognizedAtUtc = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return item with { Result = result, NeedsApi = false };
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = request.ApiMaxDegreeOfParallelism,
            BoundedCapacity = 500,
            CancellationToken = ct
        });

        // 组织文件（Copy/Move）
        var organizeBlock = new ActionBlock<PipelineItem>(async work =>
        {
            var outputRoot = request.OutputRoot;
            var (series, character) = (work.Result.Series, work.Result.Character);

            // 根据用户选择的归类方式计算目标目录。
            // 无法识别或缺失关键字段统一放入 OutputRoot/Unknown。
            string destDir;

            var mode = request.OutputOrganizationMode;
            switch (mode)
            {
                case OutputOrganizationMode.SeriesOnly:
                    destDir = (series == "Unknown" || work.Result.Status != "Success")
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot, FileNameSanitizer.SanitizeFolderName(series));
                    break;

                case OutputOrganizationMode.CharacterOnly:
                    destDir = (character == "Unknown" || work.Result.Status != "Success")
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot, FileNameSanitizer.SanitizeFolderName(character));
                    break;

                case OutputOrganizationMode.CharacterThenSeries:
                    destDir = (character == "Unknown" || series == "Unknown" || work.Result.Status != "Success")
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot,
                            FileNameSanitizer.SanitizeFolderName(character),
                            FileNameSanitizer.SanitizeFolderName(series));
                    break;

                case OutputOrganizationMode.SeriesThenCharacter:
                default:
                    destDir = (series == "Unknown" || character == "Unknown" || work.Result.Status != "Success")
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot,
                            FileNameSanitizer.SanitizeFolderName(series),
                            FileNameSanitizer.SanitizeFolderName(character));
                    break;
            }

            Directory.CreateDirectory(destDir);

            var fileName = Path.GetFileName(work.SourcePath);
            var baseDestPath = Path.Combine(destDir, fileName);
            var destPath = FileCollisionResolver.GetUniqueDestinationPath(baseDestPath);

            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        if (request.FileOperationMode == FileOperationMode.Move)
                        {
                            // Move 在后台线程执行：避免阻塞 UI。
                            await Task.Run(() => File.Move(work.SourcePath, destPath, overwrite: false), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            await CopyFileAsync(work.SourcePath, destPath, ct).ConfigureAwait(false);
                        }

                        // 成功后跳出重试循环
                        break;
                    }
                    catch
                    {
                        if (ct.IsCancellationRequested)
                            throw;

                        // 并发情况下可能发生真正的“竞态”重名：重新生成唯一文件名再尝试。
                        destPath = FileCollisionResolver.GetUniqueDestinationPath(baseDestPath);

                        if (attempt == 2)
                            break;
                    }
                }
            }
            catch
            {
                if (ct.IsCancellationRequested)
                    throw;
                // 重名/权限/文件占用等失败：不中断管道（你后续可把错误写入日志表）。
            }
        },
        new ExecutionDataflowBlockOptions
        {
            // IO 操作一般不需要太高并发，避免磁盘抖动
            MaxDegreeOfParallelism = 2,
            BoundedCapacity = 300,
            CancellationToken = ct
        });

        // 路由：
        // - hashBlock（缓存命中） -> organizeBlock
        // - hashBlock（需要 API） -> apiBlock -> organizeBlock
        hashBlock.LinkTo(organizeBlock, new DataflowLinkOptions { PropagateCompletion = true }, item => !item.NeedsApi);
        hashBlock.LinkTo(apiBlock, new DataflowLinkOptions { PropagateCompletion = true }, item => item.NeedsApi);
        apiBlock.LinkTo(organizeBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // 扫描：Directory.EnumerateFiles 流式枚举，避免一次性加载内存。
        var scannerTask = Task.Run(async () =>
        {
            try
            {
                var allowed = new HashSet<string>(SupportedExtensions, StringComparer.OrdinalIgnoreCase);

                foreach (var file in Directory.EnumerateFiles(request.InputRoot, "*.*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    var ext = Path.GetExtension(file);
                    if (!allowed.Contains(ext))
                        continue;

                    // bounded capacity 会对生产者施加背压，避免内存膨胀。
                    await fileBuffer.SendAsync(file, ct).ConfigureAwait(false);
                }

                fileBuffer.Complete();
            }
            catch (Exception ex)
            {
                ((IDataflowBlock)fileBuffer).Fault(ex);
            }
        }, ct);

        await organizeBlock.Completion.ConfigureAwait(false);
        await scannerTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Phase1：扫描 + 计算 MD5 + 调用 API 并把“完整候选集（含 box）”落地到 SQLite。
    /// 返回给确认窗口的待处理列表（默认全部标记为已确认，且默认选择候选第 1 个）。
    /// </summary>
    public async Task<List<PendingImageItem>> ScanAndRecognizeCandidatesAsync(
        PipelineRequest request,
        IProgress<PipelineProgressUpdate> progress,
        CancellationToken ct)
    {
        var stats = new ProcessingStats();

        var executor = new ThrottledHttpExecutor(request.ApiMaxDegreeOfParallelism, request.ApiMaxRps);
        var settings = new AnimeApiSettings
        {
            Url = _baseApiSettings.Url,
            ImageFormFieldName = _baseApiSettings.ImageFormFieldName,
            Base64FormFieldName = _baseApiSettings.Base64FormFieldName,
            IsMulti = _baseApiSettings.IsMulti,
            Model = _baseApiSettings.Model,
            AiDetect = _baseApiSettings.AiDetect,
            RequestMode = request.ApiRequestMode,
            BackoffBaseDelay = _baseApiSettings.BackoffBaseDelay,
            BackoffMaxDelay = _baseApiSettings.BackoffMaxDelay,
            MaxRetriesOn429 = _baseApiSettings.MaxRetriesOn429,
            DefaultMaxConcurrentRequests = request.ApiMaxDegreeOfParallelism,
            DefaultMaxRps = request.ApiMaxRps
        };

        var httpClient = _httpClientFactory.CreateClient("anime_api");
        var apiClient = new AnimeRecognitionClient(httpClient, executor, settings);

        var fileBuffer = new BufferBlock<string>(new DataflowBlockOptions
        {
            BoundedCapacity = 2000,
            CancellationToken = ct
        });

        var pendingBag = new System.Collections.Concurrent.ConcurrentBag<PendingImageItem>();

        var hashBlock = new TransformBlock<string, ScanWorkItem>(async filePath =>
        {
            stats.IncrementScanned();
            progress.Report(ToProgressUpdate(stats, executor));

            var md5 = await Md5Hasher.ComputeMd5HexAsync(filePath, ct).ConfigureAwait(false);

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var cached = await db.RecognitionCandidatesCaches.FirstOrDefaultAsync(x => x.Md5 == md5, ct).ConfigureAwait(false);

            if (cached is not null)
            {
                stats.IncrementCacheHit();
                progress.Report(ToProgressUpdate(stats, executor));

                var pending = CreatePendingFromCached(filePath, md5, cached);
                return new ScanWorkItem(pending, NeedsApi: false);
            }

            var unknownPending = new PendingImageItem
            {
                FilePath = filePath,
                Md5 = md5,
                DefaultWork = "Unknown",
                DefaultCharacter = "Unknown",
                SelectedWork = "Unknown",
                SelectedCharacter = "Unknown",
                BoxX1 = 0,
                BoxY1 = 0,
                BoxX2 = 0,
                BoxY2 = 0,
                BoxId = string.Empty,
                NotConfident = true,
                Status = PendingItemStatus.Confirmed
            };

            return new ScanWorkItem(unknownPending, NeedsApi: true);
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            BoundedCapacity = 1000,
            CancellationToken = ct
        });

        var cachedAddBlock = new ActionBlock<ScanWorkItem>(work =>
        {
            pendingBag.Add(work.Pending);
            return Task.CompletedTask;
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = 1000,
            CancellationToken = ct
        });

        var apiBlock = new TransformBlock<ScanWorkItem, PendingImageItem>(async work =>
        {
            // 防止并发重复调用：再次检查缓存。
            await using (var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
            {
                var existing = await db.RecognitionCandidatesCaches.FirstOrDefaultAsync(x => x.Md5 == work.Pending.Md5, ct).ConfigureAwait(false);
                if (existing is not null)
                {
                    var cachedPending = CreatePendingFromCached(work.Pending.FilePath, work.Pending.Md5, existing);
                    stats.IncrementCacheHit();
                    progress.Report(ToProgressUpdate(stats, executor));
                    return cachedPending;
                }
            }

            var (candidatesData, throttled429Count) = await apiClient.RecognizeCandidatesAsync(work.Pending.FilePath, ct).ConfigureAwait(false);
            if (throttled429Count > 0)
            {
                stats.AddThrottled429(throttled429Count);
                progress.Report(ToProgressUpdate(stats, executor));
            }

            var first = candidatesData.Characters.Count > 0 ? candidatesData.Characters[0] : null;
            var defaultWork = first?.Work ?? "Unknown";
            var defaultCharacter = first?.Character ?? "Unknown";

            work.Pending.DefaultWork = defaultWork;
            work.Pending.DefaultCharacter = defaultCharacter;
            work.Pending.SelectedWork = defaultWork;
            work.Pending.SelectedCharacter = defaultCharacter;

            work.Pending.BoxX1 = candidatesData.Box.Length > 0 ? candidatesData.Box[0] : 0;
            work.Pending.BoxY1 = candidatesData.Box.Length > 1 ? candidatesData.Box[1] : 0;
            work.Pending.BoxX2 = candidatesData.Box.Length > 2 ? candidatesData.Box[2] : 0;
            work.Pending.BoxY2 = candidatesData.Box.Length > 3 ? candidatesData.Box[3] : 0;
            work.Pending.BoxId = candidatesData.BoxId ?? string.Empty;
            work.Pending.NotConfident = candidatesData.NotConfident;
            work.Pending.Status = PendingItemStatus.Confirmed;

            var isUnknown = string.Equals(defaultWork, "Unknown", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(defaultCharacter, "Unknown", StringComparison.OrdinalIgnoreCase);

            if (!isUnknown)
                stats.IncrementApiSuccess();
            else
            {
                stats.IncrementApiUnknown();
                stats.IncrementApiFailures();
            }

            progress.Report(ToProgressUpdate(stats, executor));

            // 落地缓存（候选集 + box）
            await using (var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
            {
                var entity = await db.RecognitionCandidatesCaches.FirstOrDefaultAsync(x => x.Md5 == work.Pending.Md5, ct).ConfigureAwait(false);

                var candidatesJson = JsonSerializer.Serialize(candidatesData);

                if (entity is null)
                {
                    db.RecognitionCandidatesCaches.Add(new RecognitionCandidatesCacheEntity
                    {
                        Md5 = work.Pending.Md5,
                        CandidatesJson = candidatesJson,
                        FirstWork = defaultWork,
                        FirstCharacter = defaultCharacter,
                        BoxX1 = work.Pending.BoxX1,
                        BoxY1 = work.Pending.BoxY1,
                        BoxX2 = work.Pending.BoxX2,
                        BoxY2 = work.Pending.BoxY2,
                        BoxId = work.Pending.BoxId,
                        NotConfident = work.Pending.NotConfident,
                        RecognizedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    entity.CandidatesJson = candidatesJson;
                    entity.FirstWork = defaultWork;
                    entity.FirstCharacter = defaultCharacter;
                    entity.BoxX1 = work.Pending.BoxX1;
                    entity.BoxY1 = work.Pending.BoxY1;
                    entity.BoxX2 = work.Pending.BoxX2;
                    entity.BoxY2 = work.Pending.BoxY2;
                    entity.BoxId = work.Pending.BoxId;
                    entity.NotConfident = work.Pending.NotConfident;
                    entity.RecognizedAtUtc = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return work.Pending;
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = request.ApiMaxDegreeOfParallelism,
            BoundedCapacity = 500,
            CancellationToken = ct
        });

        // 路由：
        // - hashBlock（缓存命中） -> cachedAddBlock
        // - hashBlock（需要 API） -> apiBlock
        // 关键：必须 PropagateCompletion，确保最后 Completion 能正确触发，避免“永远卡在扫描中”。
        hashBlock.LinkTo(cachedAddBlock, new DataflowLinkOptions { PropagateCompletion = true }, work => !work.NeedsApi);
        hashBlock.LinkTo(apiBlock, new DataflowLinkOptions { PropagateCompletion = true }, work => work.NeedsApi);

        var apiAddBlock = new ActionBlock<PendingImageItem>(item =>
        {
            pendingBag.Add(item);
            return Task.CompletedTask;
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = 500,
            CancellationToken = ct
        });

        apiBlock.LinkTo(apiAddBlock, new DataflowLinkOptions { PropagateCompletion = true });

        fileBuffer.LinkTo(hashBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // 扫描（流式枚举）
        var scannerTask = Task.Run(async () =>
        {
            try
            {
                // 按后缀枚举：避免 "*.*" 导致遍历大量无关文件（减少“卡住”观感）
                foreach (var ext in SupportedExtensions)
                {
                    var pattern = "*" + ext; // e.g. "*.jpg"
                    foreach (var file in Directory.EnumerateFiles(request.InputRoot, pattern, SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        await fileBuffer.SendAsync(file, ct).ConfigureAwait(false);
                    }
                }

                fileBuffer.Complete();
            }
            catch (Exception ex)
            {
                ((IDataflowBlock)fileBuffer).Fault(ex);
            }
        }, ct);

        await apiAddBlock.Completion.ConfigureAwait(false);
        await cachedAddBlock.Completion.ConfigureAwait(false);
        await scannerTask.ConfigureAwait(false);

        return new List<PendingImageItem>(pendingBag);
    }

    /// <summary>
    /// Phase2：根据用户确认结果，把文件物理 Copy/Move 到目标目录。
    /// </summary>
    public async Task OrganizeConfirmedFilesAsync(
        PipelineRequest request,
        IReadOnlyList<PendingImageItem> pendingItems,
        OutputOrganizationMode outputOrganizationMode,
        CancellationToken ct)
    {
        var outputRoot = request.OutputRoot;

        var organizeBlock = new ActionBlock<PendingImageItem>(async item =>
        {
            if (item.Status != PendingItemStatus.Confirmed)
                return;

            var series = item.SelectedWork;
            var character = item.SelectedCharacter;

            // 无法识别统一放入 OutputRoot/Unknown
            string destDir;
            switch (outputOrganizationMode)
            {
                case OutputOrganizationMode.SeriesOnly:
                    destDir = (series == "Unknown" || string.IsNullOrWhiteSpace(series))
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot, FileNameSanitizer.SanitizeFolderName(series));
                    break;

                case OutputOrganizationMode.CharacterOnly:
                    destDir = (character == "Unknown" || string.IsNullOrWhiteSpace(character))
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot, FileNameSanitizer.SanitizeFolderName(character));
                    break;

                case OutputOrganizationMode.CharacterThenSeries:
                    destDir = (character == "Unknown" || series == "Unknown" || string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(series))
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot,
                            FileNameSanitizer.SanitizeFolderName(character),
                            FileNameSanitizer.SanitizeFolderName(series));
                    break;

                case OutputOrganizationMode.SeriesThenCharacter:
                default:
                    destDir = (series == "Unknown" || character == "Unknown" || string.IsNullOrWhiteSpace(series) || string.IsNullOrWhiteSpace(character))
                        ? Path.Combine(outputRoot, "Unknown")
                        : Path.Combine(outputRoot,
                            FileNameSanitizer.SanitizeFolderName(series),
                            FileNameSanitizer.SanitizeFolderName(character));
                    break;
            }

            Directory.CreateDirectory(destDir);

            var fileName = Path.GetFileName(item.FilePath);
            var baseDestPath = Path.Combine(destDir, fileName);
            var destPath = FileCollisionResolver.GetUniqueDestinationPath(baseDestPath);

            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (request.FileOperationMode == FileOperationMode.Move)
                        {
                            await Task.Run(() => File.Move(item.FilePath, destPath, overwrite: false), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            await CopyFileAsync(item.FilePath, destPath, ct).ConfigureAwait(false);
                        }
                        break;
                    }
                    catch
                    {
                        if (ct.IsCancellationRequested)
                            throw;

                        destPath = FileCollisionResolver.GetUniqueDestinationPath(baseDestPath);
                    }
                }
            }
            catch
            {
                // 忽略单文件失败，继续处理其它项。
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 2,
            BoundedCapacity = 300,
            CancellationToken = ct
        });

        foreach (var item in pendingItems)
        {
            ct.ThrowIfCancellationRequested();
            await organizeBlock.SendAsync(item, ct).ConfigureAwait(false);
        }

        organizeBlock.Complete();
        await organizeBlock.Completion.ConfigureAwait(false);
    }

    private static async Task CopyFileAsync(string sourcePath, string destPath, CancellationToken ct)
    {
        // 使用流式异步拷贝，控制内存并提升吞吐。
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var dest = new FileStream(
            destPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await source.CopyToAsync(dest, 1024 * 1024, ct).ConfigureAwait(false);
    }

    private static PipelineProgressUpdate ToProgressUpdate(ProcessingStats stats, ThrottledHttpExecutor executor) =>
        new(
            Scanned: stats.Scanned,
            CacheHits: stats.CacheHits,
            ApiSuccess: stats.ApiSuccess,
            ApiUnknown: stats.ApiUnknown,
            ApiFailures: stats.ApiFailures,
            Throttled429: stats.Throttled429,
            GlobalPauseRemainingMs: executor.GetGlobalPauseRemainingMs());

    private sealed record PipelineItem(
        string SourcePath,
        string Md5,
        RecognitionResult Result,
        bool NeedsApi);

    private sealed record ScanWorkItem(PendingImageItem Pending, bool NeedsApi);

    private static PendingImageItem CreatePendingFromCached(string filePath, string md5, RecognitionCandidatesCacheEntity cached)
    {
        var pending = new PendingImageItem
        {
            FilePath = filePath,
            Md5 = md5,
            DefaultWork = cached.FirstWork ?? "Unknown",
            DefaultCharacter = cached.FirstCharacter ?? "Unknown",
            SelectedWork = cached.FirstWork ?? "Unknown",
            SelectedCharacter = cached.FirstCharacter ?? "Unknown",
            BoxX1 = cached.BoxX1,
            BoxY1 = cached.BoxY1,
            BoxX2 = cached.BoxX2,
            BoxY2 = cached.BoxY2,
            BoxId = cached.BoxId ?? string.Empty,
            NotConfident = cached.NotConfident,
            Status = PendingItemStatus.Confirmed
        };
        return pending;
    }
}

