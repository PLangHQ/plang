using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.modules;

/// <summary>
/// Capability interface — declares that an action writes to channels.
/// The engine wires Channels from the context's actor during action setup.
/// Actions navigate: Channels.WriteAsync(channel, content).
/// </summary>
public interface IChannel
{
    PLang.Runtime2.Engine.Channels.@this Channels { get; set; }
}
