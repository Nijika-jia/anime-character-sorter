using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeSorterWin.Services.Api;

/// <summary>
/// 统一的 HTTP 节流执行器：
/// - 全局 RPS 限制（无鉴权接口，防止突发打爆）
/// - 并发限制（MaxDegreeOfParallelism）
/// - 收到 429 后触发全局暂停，并在调用方的重试循环中做指数退避
/// </summary>
public sealed class ThrottledHttpExecutor
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly object _rpsLock = new();
    private readonly TimeSpan _minInterval;

    private readonly object _pauseLock = new();
    private DateTime _pauseUntilUtc = DateTime.MinValue;

    public ThrottledHttpExecutor(int maxConcurrentRequests, double maxRps)
    {
        if (maxConcurrentRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests));
        if (maxRps <= 0) throw new ArgumentOutOfRangeException(nameof(maxRps));

        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        _minInterval = TimeSpan.FromSeconds(1.0 / maxRps);
    }

    public Task WaitIfPausedAsync(CancellationToken ct)
    {
        DateTime untilUtc;
        lock (_pauseLock)
        {
            untilUtc = _pauseUntilUtc;
        }

        var delay = untilUtc - DateTime.UtcNow;
        if (delay <= TimeSpan.Zero)
            return Task.CompletedTask;

        return Task.Delay(delay, ct);
    }

    public void TriggerGlobalPause(TimeSpan pauseDuration)
    {
        if (pauseDuration <= TimeSpan.Zero)
            return;

        var until = DateTime.UtcNow.Add(pauseDuration);
        lock (_pauseLock)
        {
            if (until > _pauseUntilUtc)
                _pauseUntilUtc = until;
        }
    }

    /// <summary>
    /// 获取“全局暂停”的剩余时间（毫秒），用于 UI 显示。
    /// </summary>
    public long GetGlobalPauseRemainingMs()
    {
        DateTime untilUtc;
        lock (_pauseLock)
        {
            untilUtc = _pauseUntilUtc;
        }

        var remaining = untilUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return 0;

        return (long)Math.Ceiling(remaining.TotalMilliseconds);
    }

    /// <summary>
    /// 在发起实际请求前等待：并发名额、RPS 间隔、以及全局 429 暂停。
    /// </summary>
    public async Task<SemaphoreSlim> WaitForTurnAsync(CancellationToken ct)
    {
        await _concurrencySemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 保证全局 RPS：不管并发是多少，都要让每次请求至少间隔 _minInterval。
            var delay = GetDelayForRps();
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct).ConfigureAwait(false);

            await WaitIfPausedAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            _concurrencySemaphore.Release();
            throw;
        }

        return _concurrencySemaphore;
    }

    public void ReleaseAfterRequest()
    {
        _concurrencySemaphore.Release();
    }

    private TimeSpan GetDelayForRps()
    {
        lock (_rpsLock)
        {
            // 通过“下次允许时间”来实现全局节流
            if (_nextAllowedUtc == DateTime.MinValue)
                _nextAllowedUtc = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            if (now < _nextAllowedUtc)
            {
                var delay = _nextAllowedUtc - now;
                _nextAllowedUtc = _nextAllowedUtc + _minInterval;
                return delay;
            }

            _nextAllowedUtc = now + _minInterval;
            return TimeSpan.Zero;
        }
    }

    private DateTime _nextAllowedUtc = DateTime.MinValue;
}

