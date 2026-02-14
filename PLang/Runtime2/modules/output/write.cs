using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.output;

[Action("write", Cacheable = false)]
public partial class Write : IContext
{
    public partial object Content { get; init; }

    [Default("user")]
    public partial Actor Actor { get; init; }

    [Default("default")]
    public partial string? Channel { get; init; }

    public async Task<Data> Run()
    {
        var channel = string.IsNullOrEmpty(Channel) ? "default" : Channel;
        return await Actor.Channels.WriteAsync(channel, Content);
    }
}
