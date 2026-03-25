using System.ComponentModel.DataAnnotations;

namespace AnimeSorterWin.Data.Entities;

/// <summary>
/// 缓存完整候选集：候选 work/character + box + not_confident。
/// </summary>
public sealed class RecognitionCandidatesCacheEntity
{
    [Key]
    public string Md5 { get; set; } = string.Empty;

    // 完整候选 JSON（用于下拉/手动输入/绘制 box）
    public string CandidatesJson { get; set; } = string.Empty;

    // 为了快速构建默认候选与确认窗口展示，额外存首个 work/character 与 box。
    public string FirstWork { get; set; } = "Unknown";
    public string FirstCharacter { get; set; } = "Unknown";

    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public double BoxX2 { get; set; }
    public double BoxY2 { get; set; }

    public string BoxId { get; set; } = string.Empty;
    public bool NotConfident { get; set; }

    public DateTime RecognizedAtUtc { get; set; } = DateTime.UtcNow;
}

