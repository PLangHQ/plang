namespace app.modules.timer;

/// <summary>
/// Starts a named timer. Records the start time in static storage so a later
/// <c>timer.end</c> with the same name (or no name, to target the last started) returns
/// the elapsed duration. Use for performance tracing and simple stopwatch measurements.
/// </summary>
[System.ComponentModel.Description("Record a named start time so a later timer.end can compute elapsed duration")]
[Action("start", Cacheable = false)]
public partial class Start : IContext, IStatic
{
    public partial data.@this<string>? Name { get; init; }
    [Default("goal")]
    public partial data.@this<string> Scope { get; init; }

    public Task<data.@this> Run()
    {
        var key = Name?.Value ?? "default";
        var entry = new TimerEntry(DateTimeOffset.UtcNow, Scope.Value!);
        Static[key] = entry;
        Static["__last__"] = key;
        return Task.FromResult(Data(true));
    }
}

public record TimerEntry(DateTimeOffset StartedAt, string Scope);
