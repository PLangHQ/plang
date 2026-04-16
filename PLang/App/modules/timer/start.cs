namespace App.modules.timer;

[Action("start", Cacheable = false)]
public partial class Start : IContext, IStatic
{
    public partial Data.@this<string>? Name { get; init; }
    [Default("goal")]
    public partial Data.@this<string> Scope { get; init; }

    public Task<Data.@this> Run()
    {
        var key = Name?.Value ?? "default";
        var entry = new TimerEntry(DateTimeOffset.UtcNow, Scope.Value!);
        Static[key] = entry;
        Static["__last__"] = key;
        return Task.FromResult(Data(true));
    }
}

public record TimerEntry(DateTimeOffset StartedAt, string Scope);
