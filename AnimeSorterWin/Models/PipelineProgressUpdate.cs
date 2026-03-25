namespace AnimeSorterWin.Models;

public sealed record PipelineProgressUpdate(
    long Scanned,
    long CacheHits,
    long ApiSuccess,
    long ApiUnknown,
    long ApiFailures,
    long Throttled429
);

