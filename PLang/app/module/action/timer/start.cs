namespace app.module.action.timer;

/// <summary>
/// Starts a named timer. Records the start time in static storage so a later
/// <c>timer.end</c> with the same name (or no name, to target the last started) returns
/// the elapsed duration. Use for performance tracing and simple stopwatch measurements.
/// </summary>
[Action("start", Cacheable = false)]
public partial class Start : IContext, IStatic
{
    public partial data.@this<global::app.type.item.text.@this>? Name { get; init; }
    [Default("goal")]
    public partial data.@this<global::app.type.item.text.@this> Scope { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Run()
    {
        var key = Name == null ? "default" : await Name.Clr<string>("default");
        var entry = new TimerEntry(DateTimeOffset.UtcNow, (await Scope.Value())!.Clr<string>()!);
        Static[key] = entry;
        Static["__last__"] = key;
        return Context.Ok<global::app.type.item.@bool.@this>(true);
    }
}

public record TimerEntry(DateTimeOffset StartedAt, string Scope);
