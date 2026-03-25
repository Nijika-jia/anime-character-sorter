using AnimeSorterWin.Services.Api;

namespace AnimeSorterWin.Models;

public sealed class PipelineRequest
{
    public required string InputRoot { get; init; }
    public required string OutputRoot { get; init; }

    public required int ApiMaxDegreeOfParallelism { get; init; }
    public required double ApiMaxRps { get; init; }

    public required FileOperationMode FileOperationMode { get; init; }

    public required ApiRequestMode ApiRequestMode { get; init; }

    /// <summary>
    /// 输出目录归类方式。
    /// </summary>
    public required OutputOrganizationMode OutputOrganizationMode { get; init; }
}

