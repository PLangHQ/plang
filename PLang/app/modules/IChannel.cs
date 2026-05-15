using app.Actor.Context;

namespace app.modules;

/// <summary>
/// Capability interface — declares that an action targets a single resolved channel.
/// Source-gen reads the action's "channel" parameter, resolves it via
/// <see cref="app.Channels.@this.Resolve"/> on the current actor's Channels, and
/// injects the resolved <see cref="app.Channels.Channel.@this"/> instance here.
///
/// Actions navigate: Channel.WriteAsync(envelope) / Channel.Ask(prompt) / etc.
/// </summary>
public interface IChannel
{
    app.Channels.Channel.@this Channel { get; set; }
}
