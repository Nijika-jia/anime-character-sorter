namespace AnimeSorterWin.Models;

public enum PendingItemStatus
{
    Confirmed,
    Skipped,
}

/// <summary>
/// 扫描后、用于确认窗口展示的待处理条目。
/// </summary>
public sealed class PendingImageItem
{
    public required string FilePath { get; init; }
    public required string Md5 { get; init; }

    // 默认（候选第 1 个）：
    public required string DefaultWork { get; set; }
    public required string DefaultCharacter { get; set; }

    // 当前确认选择（默认已确认并使用默认候选）。
    public string SelectedWork { get; set; } = string.Empty;
    public string SelectedCharacter { get; set; } = string.Empty;

    public PendingItemStatus Status { get; set; } = PendingItemStatus.Confirmed;

    public string StatusDisplay => Status switch
    {
        PendingItemStatus.Confirmed => "已确认",
        PendingItemStatus.Skipped => "已跳过",
        _ => Status.ToString()
    };

    // 用于详情区域绘制 box（归一化坐标）
    public double BoxX1 { get; set; }
    public double BoxY1 { get; set; }
    public double BoxX2 { get; set; }
    public double BoxY2 { get; set; }

    public string BoxId { get; set; } = string.Empty;
    public bool NotConfident { get; set; }

    public void ResetToDefault()
    {
        SelectedWork = DefaultWork;
        SelectedCharacter = DefaultCharacter;
        Status = PendingItemStatus.Confirmed;
    }
}

