using System.ComponentModel.DataAnnotations;

namespace AnimeSorterWin.Data.Entities;

/// <summary>
/// 用于持久化缓存：key 为文件内容 MD5，value 为识别结果。
/// </summary>
public sealed class RecognitionCacheEntity
{
    /// <summary>
    /// 文件内容 MD5（作为主键避免重复识别）。
    /// </summary>
    [Key]
    public string Md5 { get; set; } = string.Empty;

    /// <summary>
    /// 识别到的作品（Series）。
    /// </summary>
    public string Series { get; set; } = "Unknown";

    /// <summary>
    /// 识别到的角色（Character）。
    /// </summary>
    public string Character { get; set; } = "Unknown";

    /// <summary>
    /// 识别类型：Success/Unknown/Error 等。
    /// </summary>
    public string ResultStatus { get; set; } = "Unknown";

    public DateTime RecognizedAtUtc { get; set; } = DateTime.UtcNow;
}

