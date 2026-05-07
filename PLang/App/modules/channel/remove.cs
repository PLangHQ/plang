using App.Errors;

namespace App.modules.channel;

/// <summary>
/// Unregisters a channel by name. Refuses for the default channels
/// (<see cref="App.Channels.@this.Defaults"/> — output/error/input) which the
/// boot invariant requires; use <c>channel.set</c> to re-bind their backing.
/// PLang surface:
///   - remove channel "logger"
/// </summary>
[System.ComponentModel.Description("Remove a channel by name. Default channels (output/error/input) cannot be removed.")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial Data.@this<string> Name { get; init; }
    public partial Data.@this<global::App.Actor.@this>? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var name = Name.Value;
        if (string.IsNullOrEmpty(name))
            return global::App.Data.@this.FromError(new ServiceError("Channel name is required", "ValueRequired", 400));

        if (global::App.Channels.@this.Defaults.Any(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase)))
            return global::App.Data.@this.FromError(new ServiceError(
                $"Channel '{name}' is a default channel and cannot be removed (use channel.set to replace its backing).",
                "ChannelInvariantViolation", 400));

        var actor = Actor?.Value ?? Context.Actor;
        var removed = await actor.Channels.RemoveAsync(name);
        if (!removed)
            return global::App.Data.@this.FromError(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404));
        return global::App.Data.@this.Ok();
    }
}
