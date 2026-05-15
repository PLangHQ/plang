namespace app.modules.timer;

/// <summary>
/// Stops a previously started timer and returns the elapsed <see cref="TimeSpan"/>.
/// If Name is omitted, ends the most recently started timer. Returns a
/// <see cref="app.Errors.ValidationError"/> if no timer has been started or the
/// named timer is unknown.
/// </summary>
[ModuleDescription("Named stopwatch timers for measuring elapsed time across steps")]
[System.ComponentModel.Description("Stop a named timer and return the elapsed TimeSpan since it was started")]
[Action("end", Cacheable = false)]
public partial class End : IContext, IStatic
{
    public partial Data.@this<string>? Name { get; init; }

    public Task<Data.@this> Run()
    {
        // If no name given, use the last started timer
        var key = Name?.Value;
        if (key == null)
        {
            if (!Static.TryGetValue("__last__", out var lastObj) || lastObj is not string lastKey)
                return Task.FromResult(Error(
                    new app.Errors.ValidationError("No timer has been started")));
            key = lastKey;
        }

        if (!Static.TryGetValue(key, out var entryObj) || entryObj is not TimerEntry entry)
            return Task.FromResult(Error(
                new app.Errors.ValidationError($"Timer '{key}' was not started")));

        var elapsed = DateTimeOffset.UtcNow - entry.StartedAt;
        Static.TryRemove(key, out _);
        return Task.FromResult(Data(elapsed));
    }
}
