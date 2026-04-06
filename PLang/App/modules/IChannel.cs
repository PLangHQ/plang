using App.Context;

namespace App.modules;

/// <summary>
/// Capability interface — declares that an action writes to channels.
/// The engine wires Channels from the context's actor during action setup.
/// Actions navigate: Channels.WriteAsync(channel, content).
/// </summary>
public interface IChannel
{
    App.Channels.@this Channels { get; set; }
}
