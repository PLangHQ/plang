using App.Actor.Context;

namespace App.modules;

/// <summary>
/// Capability interface — declares that an action targets a single resolved channel.
/// Source-gen reads the action's "channel" parameter, resolves it via
/// <see cref="App.Channels.@this.Resolve"/> on the current actor's Channels, and
/// injects the resolved <see cref="App.Channels.Channel.@this"/> instance here.
///
/// Actions navigate: Channel.WriteAsync(envelope) / Channel.Ask(prompt) / etc.
/// </summary>
public interface IChannel
{
    App.Channels.Channel.@this Channel { get; set; }
}
