using app.error;

namespace app.module.action.channel;

/// <summary>
/// Unregisters a channel by name. Refuses for the default channels
/// (<see cref="app.channel.list.@this.Defaults"/> — output/error/input) which the
/// boot invariant requires; use <c>channel.set</c> to re-bind their backing.
/// PLang surface:
///   - remove channel "logger"
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }
    public partial data.@this<global::app.actor.@this>? Actor { get; init; }

    public async Task<data.@this> Run()
    {
        var name = (await Name.Value())?.Clr<string>();
        if (string.IsNullOrEmpty(name))
            return Context.Error(new ServiceError("Channel name is required", "ValueRequired", 400));

        if (global::app.channel.list.@this.Defaults.Any(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase)))
            return Context.Error(new ServiceError(
                $"Channel '{name}' is a default channel and cannot be removed (use channel.set to replace its backing).",
                "ChannelInvariantViolation", 400));

        var actor = (Actor == null ? null : await Actor.Value()) ?? Context.Actor;
        var removed = await actor.Channel.RemoveAsync(name);
        if (!removed)
            return Context.Error(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404));
        return Context.Ok();
    }
}
