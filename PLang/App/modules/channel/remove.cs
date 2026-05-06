using App.Errors;
using App.Variables;

namespace App.modules.channel;

/// <summary>
/// Unregisters a custom-named channel. Refuses to remove the standard role-channels
/// (output / error / input) — those are entry-point invariants. Use
/// <c>channel.set</c> to replace a role channel's backing.
/// PLang surface:
///   - remove channel "logger"
/// </summary>
[System.ComponentModel.Description("Remove a custom-named channel. Role-channels (output/error/input) cannot be removed.")]
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial Data.@this<string> Name { get; init; }
    public partial Data.@this<App.Variables.Variable>? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var name = Name.Value;
        if (string.IsNullOrEmpty(name))
            return global::App.Data.@this.FromError(new ServiceError("Channel name is required", "ValueRequired", 400));

        var lower = name.ToLowerInvariant();
        if (lower == global::App.Channels.@this.Output
            || lower == global::App.Channels.@this.Error
            || lower == global::App.Channels.@this.Input)
            return global::App.Data.@this.FromError(new ServiceError(
                $"Channel '{name}' is a role-channel and cannot be removed (use channel.set to replace).",
                "ChannelInvariantViolation", 400));

        var (actor, actorErr) = Set.ResolveTargetActor(Context, Actor);
        if (actorErr != null) return actorErr;

        var removed = await actor!.Channels.RemoveAsync(name);
        if (!removed)
            return global::App.Data.@this.FromError(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404));
        return global::App.Data.@this.Ok();
    }
}
