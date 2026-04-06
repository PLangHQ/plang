using App.Variables;

namespace App.Cache;

/// <summary>
/// Pluggable cache interface for step result caching.
/// Default: MemoryStepCache. Swap via: - use 'redis.dll' for caching
/// </summary>
public interface ICache
{
    Task<Data.@this?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, Data.@this value, CacheSettings settings, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Atomic add-if-absent. Returns true if the key was added (new), false if it already existed.
    /// Used for nonce replay prevention.
    /// </summary>
    Task<bool> TryAddAsync(string key, Data.@this value, CacheSettings settings, CancellationToken ct = default);
}
