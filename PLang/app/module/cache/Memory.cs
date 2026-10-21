using System.Runtime.Caching;

namespace app.module.cache;

/// <summary>
/// Default in-memory ICache implementation using System.Runtime.Caching.MemoryCache.
/// Stores Data references directly — no serialization overhead.
/// </summary>
public sealed class Memory : ICache
{
    // The cache, opened when something is first cached: an app that caches nothing never opens one (its expiry
    // buckets and timers), and a lookup in a cache never opened finds nothing. Opened once — a second would run
    // timers for nothing.
    private MemoryCache? _opened;
    private object? _gate;
    private MemoryCache Cache => System.Threading.LazyInitializer.EnsureInitialized(ref _opened, ref _gate,
        () => new MemoryCache("StepCache_" + Guid.NewGuid().ToString("N")[..8]));

    public Task<data.@this?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(System.Threading.Volatile.Read(ref _opened)?.Get(key) as data.@this);

    public Task SetAsync(string key, data.@this value, CacheSettings settings, CancellationToken ct = default)
    {
        var policy = new CacheItemPolicy();
        if (settings.Sliding == true)
            policy.SlidingExpiration = TimeSpan.FromMilliseconds(settings.DurationMs);
        else
            policy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(settings.DurationMs);

        Cache.Set(key, value, policy);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        System.Threading.Volatile.Read(ref _opened)?.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(string key, data.@this value, CacheSettings settings, CancellationToken ct = default)
    {
        var policy = new CacheItemPolicy();
        if (settings.Sliding == true)
            policy.SlidingExpiration = TimeSpan.FromMilliseconds(settings.DurationMs);
        else
            policy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(settings.DurationMs);

        // AddOrGetExisting returns null if the key didn't exist (meaning it was added)
        var existing = Cache.AddOrGetExisting(key, value, policy);
        return Task.FromResult(existing == null);
    }

    /// <summary>The cache let go with its app: a MemoryCache's expiry timers keep it — and every entry it holds —
    /// alive past its app until it is disposed.</summary>
    public void Dispose() => System.Threading.Interlocked.Exchange(ref _opened, null)?.Dispose();
}
