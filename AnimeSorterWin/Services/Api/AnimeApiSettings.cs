namespace AnimeSorterWin.Services.Api;

public enum ApiRequestMode
{
    /// <summary>
    /// 使用 multipart/form-data 上传图片（推荐，避免把整张图塞进内存）。
    /// </summary>
    MultipartFormData,

    /// <summary>
    /// 把图片转成 Base64 然后发送（更吃内存，不推荐但提供兼容）。
    /// </summary>
    Base64
}

/// <summary>
/// 外部 API 配置。注意：实际运行时你需要把 Url/字段名改成你的接口要求。
/// </summary>
public sealed class AnimeApiSettings
{
    /// <summary>
    /// 动漫识别接口地址。
    /// </summary>
    public string Url { get; set; } = "https://api.animetrace.com/v1/search";

    /// <summary>
    /// multipart/form-data 中的图片文件字段名：API 文档必填参数名为 `file`。
    /// </summary>
    public string ImageFormFieldName { get; set; } = "file";

    /// <summary>
    /// multipart/form-data 中的 Base64 字段名：API 文档必填参数名为 `base64`。
    /// </summary>
    public string Base64FormFieldName { get; set; } = "base64";

    /// <summary>
    /// 是否返回多个识别结果（默认 1）。
    /// </summary>
    public int IsMulti { get; set; } = 1;

    /// <summary>
    /// 识别模型（默认 animetrace_high_beta，对应“测试版最高质量模型”）。
    /// </summary>
    public string Model { get; set; } = "animetrace_high_beta";

    /// <summary>
    /// 是否开启 AI 检测：1 开启 / 2 关闭（默认 1）。
    /// </summary>
    public int AiDetect { get; set; } = 1;

    public ApiRequestMode RequestMode { get; set; } = ApiRequestMode.MultipartFormData;

    /// <summary>
    /// 429 第一次等待时长（用户选择的 A 策略：5s）。
    /// </summary>
    public TimeSpan BackoffBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 指数退避最大等待（用户选择的 A 策略：60s）。
    /// </summary>
    public TimeSpan BackoffMaxDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 429 重试次数（用户选择的 A 策略：最多重试 5 次）。
    /// </summary>
    public int MaxRetriesOn429 { get; set; } = 5;

    /// <summary>
    /// 最大并发（由 UI 并发滑块决定），这里给默认值用于初始化。
    /// </summary>
    public int DefaultMaxConcurrentRequests { get; set; } = 3;

    /// <summary>
    /// 最大 RPS（用户选择默认预设：2 RPS）。
    /// </summary>
    public double DefaultMaxRps { get; set; } = 2;
}

