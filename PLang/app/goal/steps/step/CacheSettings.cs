namespace app.goal.steps.step;

/// <summary>
/// Cache settings for a step result.
/// </summary>
public sealed class CacheSettings
{
    [Store]
    public long DurationMs { get; init; }

    [Store]
    public bool? Sliding { get; init; }

    [Store]
    public string? Key { get; init; }

    [Store]
    public string? Location { get; init; }
}
