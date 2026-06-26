namespace app.module.timer;

/// <summary>
/// Stops a previously started timer and returns the elapsed <see cref="TimeSpan"/>.
/// If Name is omitted, ends the most recently started timer. Returns a
/// <see cref="app.error.ValidationError"/> if no timer has been started or the
/// named timer is unknown.
/// </summary>
[Action("end", Cacheable = false)]
public partial class End : IContext, IStatic
{
    public partial data.@this<global::app.type.text.@this>? Name { get; init; }

    public Task<data.@this<global::app.type.duration.@this>> Run()
    {
        // If no name given, use the last started timer
        var key = (Name?.Peek() as global::app.type.text.@this)?.ToString();
        if (key == null)
        {
            if (!Static.TryGetValue("__last__", out var lastObj) || lastObj is not string lastKey)
                return Task.FromResult(Context.Error<global::app.type.duration.@this>(
                    new app.error.ValidationError("No timer has been started")));
            key = lastKey;
        }

        if (!Static.TryGetValue(key, out var entryObj) || entryObj is not TimerEntry entry)
            return Task.FromResult(Context.Error<global::app.type.duration.@this>(
                new app.error.ValidationError($"Timer '{key}' was not started")));

        var elapsed = DateTimeOffset.UtcNow - entry.StartedAt;
        Static.TryRemove(key, out _);
        return Task.FromResult(Context.Ok<global::app.type.duration.@this>(elapsed));
    }
}
