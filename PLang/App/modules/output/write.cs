using App.Variables;

namespace App.modules.output;

[Example("write out 'hello'", "Data=hello")]
[Example("write trace 'debug info'", "Data=debug info, Properties={channel: trace}")]
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run() => await Channels.WriteAsync(this);
}
