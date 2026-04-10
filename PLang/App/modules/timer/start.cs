namespace App.modules.timer;

[Action("start", Cacheable = false)]
public partial class Start : IContext, IStatic
{
    public partial string? Name { get; init; }
    [Default("goal")]
    public partial string Scope { get; init; }

    public Task<Data.@this> Run()
    {
        var key = Name ?? "default";
        var entry = new TimerEntry(DateTimeOffset.UtcNow, Scope);
        Static[key] = entry;
        Static["__last__"] = key;
        return Task.FromResult(Data(true));
    }
}

public record TimerEntry(DateTimeOffset StartedAt, string Scope);
