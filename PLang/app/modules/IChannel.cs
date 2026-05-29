using app.actor.context;

namespace app.modules;

/// <summary>
/// Capability interface — declares that an action targets a single resolved channel.
/// Source-gen reads the action's "channel" parameter, resolves it via
/// <see cref="app.channel.list.@this.Resolve"/> on the current actor's Channels, and
/// injects the resolved <see cref="app.channel.@this"/> instance here.
///
/// Actions navigate: Channel.WriteAsync(data) / Channel.Ask(prompt) / etc.
/// </summary>
public interface IChannel
{
    app.channel.@this Channel { get; set; }
}
