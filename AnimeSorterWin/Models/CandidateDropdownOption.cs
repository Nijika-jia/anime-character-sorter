namespace AnimeSorterWin.Models;

/// <summary>
/// 下拉框选项：展示字符串 + 对应 work/character。
/// </summary>
public sealed record CandidateDropdownOption(
    string Display,
    string Work,
    string Character);

