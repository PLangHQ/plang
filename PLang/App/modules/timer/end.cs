namespace App.modules.timer;

[Action("end", Cacheable = false)]
public partial class End : IContext, IStatic
{
    public partial string? Name { get; init; }

    public Task<Data.@this> Run()
    {
        var key = Name ?? "default";
        if (!Static.TryGetValue(key, out var startObj) || startObj is not DateTimeOffset startTime)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Timer '{key}' was not started")));

        var elapsed = DateTimeOffset.UtcNow - startTime;
        Static.TryRemove(key, out _);
        return Task.FromResult(Data(elapsed));
    }
}
