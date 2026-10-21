namespace app.module.cache;

/// <summary>
/// Pluggable cache interface for step result caching.
/// Default: global::app.module.cache.Memory. Swap via: - use 'redis.dll' for caching
/// </summary>
public interface ICache : System.IDisposable
{
    /// <summary>Let go with the app that owns it: what it holds and the timers that expire it. A cache that holds
    /// nothing of its own (one outside the process) has nothing to let go.</summary>
    void System.IDisposable.Dispose() { }

    Task<data.@this?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, data.@this value, CacheSettings settings, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Atomic add-if-absent. Returns true if the key was added (new), false if it already existed.
    /// Used for nonce replay prevention.
    /// </summary>
    Task<bool> TryAddAsync(string key, data.@this value, CacheSettings settings, CancellationToken ct = default);
}
