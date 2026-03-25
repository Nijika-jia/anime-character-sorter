namespace AnimeSorterWin.Models;

/// <summary>
/// API 识别结果的统一表示。
/// </summary>
public sealed record RecognitionResult(
    string Series,
    string Character,
    string Status
);

