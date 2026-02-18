using System.Runtime.Caching;

namespace PLang.Runtime2.Engine.Cache;

/// <summary>
/// Default in-memory ICache implementation using System.Runtime.Caching.MemoryCache.
/// Stores object references directly — no serialization overhead.
/// </summary>
public sealed class MemoryStepCache : ICache
{
    private readonly MemoryCache _cache = new MemoryCache("StepCache_" + Guid.NewGuid().ToString("N")[..8]);

    public Task<object?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.Get(key));

    public Task SetAsync(string key, object value, CacheSettings settings, CancellationToken ct = default)
    {
        var policy = new CacheItemPolicy();
        if (settings.Sliding)
            policy.SlidingExpiration = TimeSpan.FromSeconds(settings.DurationSeconds);
        else
            policy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(settings.DurationSeconds);

        _cache.Set(key, value, policy);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
