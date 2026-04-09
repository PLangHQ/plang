using App.Variables;

namespace App.modules.output;

/// <summary>
/// Writes data to the actor's output channel.
/// Channel selection is handled by the IChannel interface — default is the actor's primary channel.
/// </summary>
[Example("write out 'hello'", "Data=hello")]
[Example("write trace 'debug info'", "Data=debug info, Properties={channel: trace}")]
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run() => await Channels.WriteAsync(this);
}
