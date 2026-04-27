using App.Variables;

namespace App.modules.output;

/// <summary>
/// Writes data to the actor's output channel.
/// Channel selection is handled by the IChannel interface — default is the actor's primary channel.
/// </summary>
[ModuleDescription("Send text or data to an output channel (console, trace, UI, etc.)")]
[System.ComponentModel.Description("Write Data to the actor's current output channel (default: console or configured channel)")]
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run() => await Channels.WriteAsync(this);
}
