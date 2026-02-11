using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.output;

[Action("write")]
public partial class Write : IContext
{
    public partial object Content { get; init; }

    [Default("user")]
    public partial string? Actor { get; init; }

    [Default("default")]
    public partial string? Channel { get; init; }

    public async Task<Data> Run()
    {
        var actor = string.IsNullOrEmpty(Actor) ? "user" : Actor;
        var channel = string.IsNullOrEmpty(Channel) ? "default" : Channel;
        return await Context.Engine!.IO.WriteAsync(actor, channel, Content);
    }
}
