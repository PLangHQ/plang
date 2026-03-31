using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.output;

[Example("write out 'hello'", "Data=hello")]
[Example("write trace 'debug info'", "Data=debug info, Properties={channel: trace}")]
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial Data Data { get; init; }

    public async Task<Data> Run() => await Channels.WriteAsync(this);
}
