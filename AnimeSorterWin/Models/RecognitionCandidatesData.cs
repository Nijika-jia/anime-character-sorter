namespace AnimeSorterWin.Models;

/// <summary>
/// 单次 API 返回的候选集合：包含 box(not_crop) 与候选 work/character。
/// </summary>
public sealed record RecognitionCandidatesData(
    double[] Box,      // 归一化坐标 [x1, y1, x2, y2]
    string BoxId,
    bool NotConfident,
    IReadOnlyList<CandidateWorkCharacter> Characters);

