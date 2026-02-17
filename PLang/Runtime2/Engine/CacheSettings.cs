namespace PLang.Runtime2.Engine;

/// <summary>
/// Cache settings for a step result.
/// </summary>
public sealed class CacheSettings
{
    /// <summary>
    /// How long to cache the result in seconds.
    /// </summary>
    public long DurationSeconds { get; init; }

    /// <summary>
    /// Whether this is a sliding expiration (resets on access).
    /// </summary>
    public bool Sliding { get; init; }

    /// <summary>
    /// Optional custom cache key.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Where to store the cache: "memory" or "disk".
    /// </summary>
    public string? Location { get; init; }
}
