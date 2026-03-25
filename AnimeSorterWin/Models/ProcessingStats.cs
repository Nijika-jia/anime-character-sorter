using System.Threading;

namespace AnimeSorterWin.Models;

public sealed class ProcessingStats
{
    private long _scanned;
    private long _cacheHits;
    private long _apiSuccess;
    private long _apiUnknown;
    private long _apiFailures;
    private long _throttled429;

    public long Scanned => Interlocked.Read(ref _scanned);
    public long CacheHits => Interlocked.Read(ref _cacheHits);
    public long ApiSuccess => Interlocked.Read(ref _apiSuccess);
    public long ApiUnknown => Interlocked.Read(ref _apiUnknown);
    public long ApiFailures => Interlocked.Read(ref _apiFailures);
    public long Throttled429 => Interlocked.Read(ref _throttled429);

    public void IncrementScanned() => Interlocked.Increment(ref _scanned);
    public void IncrementCacheHit() => Interlocked.Increment(ref _cacheHits);
    public void IncrementApiSuccess() => Interlocked.Increment(ref _apiSuccess);
    public void IncrementApiUnknown() => Interlocked.Increment(ref _apiUnknown);
    public void IncrementApiFailures() => Interlocked.Increment(ref _apiFailures);
    public void AddThrottled429(long value) => Interlocked.Add(ref _throttled429, value);
}

