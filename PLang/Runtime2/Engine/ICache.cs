namespace PLang.Runtime2.Engine;

/// <summary>
/// Pluggable cache interface for step result caching.
/// Default: MemoryStepCache. Swap via: - use 'redis.dll' for caching
/// </summary>
public interface ICache
{
    Task<object?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, object value, CacheSettings settings, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
