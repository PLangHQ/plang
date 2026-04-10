namespace App.modules.timer;

[Action("start", Cacheable = false)]
public partial class Start : IContext, IStatic
{
    public partial string? Name { get; init; }

    public Task<Data.@this> Run()
    {
        var key = Name ?? "default";
        Static[key] = DateTimeOffset.UtcNow;
        return Task.FromResult(Data(true));
    }
}
