using System.Runtime.Caching;
using app.Variables;

namespace app.modules.cache;

/// <summary>
/// Default in-memory ICache implementation using System.Runtime.Caching.MemoryCache.
/// Stores Data references directly — no serialization overhead.
/// </summary>
public sealed class Memory : ICache
{
    private readonly MemoryCache _cache = new MemoryCache("StepCache_" + Guid.NewGuid().ToString("N")[..8]);

    public Task<data.@this?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.Get(key) as data.@this);

    public Task SetAsync(string key, data.@this value, CacheSettings settings, CancellationToken ct = default)
    {
        var policy = new CacheItemPolicy();
        if (settings.Sliding == true)
            policy.SlidingExpiration = TimeSpan.FromMilliseconds(settings.DurationMs);
        else
            policy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(settings.DurationMs);

        _cache.Set(key, value, policy);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
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
        var existing = _cache.AddOrGetExisting(key, value, policy);
        return Task.FromResult(existing == null);
    }
}
