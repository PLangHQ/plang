using App.Errors;
using App.Variables;

namespace App.modules.channel;

/// <summary>
/// Produces a signed migration envelope for a registered channel so it can be
/// shipped to another identity-aware runtime. Cross-device transport is
/// deferred (cool.md) — this action returns the envelope; a transport plug-in
/// will eventually carry it. Message-typed channels return a typed
/// <c>NotMigratable</c> error (one-shot has no state to migrate).
/// PLang surface:
///   - migrate channel "chat" to %targetIdentity%, write to %envelope%
/// </summary>
[System.ComponentModel.Description("Produce a signed migration envelope for a registered channel.")]
[Action("migrate", Cacheable = false)]
public partial class Migrate : IContext
{
    public partial Data.@this<string> Name { get; init; }
    public partial Data.@this<App.Variables.Variable>? Target { get; init; }
    public partial Data.@this<global::App.Actor.@this>? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var name = Name.Value;
        if (string.IsNullOrEmpty(name))
            return global::App.Data.@this.FromError(new ServiceError("Channel name is required", "ValueRequired", 400));

        var actor = Actor?.Value ?? Context.Actor;

        var ch = actor.Channels.Resolve(name);
        if (ch == null)
            return global::App.Data.@this.FromError(new ServiceError(
                $"Channel '{name}' not found", "ChannelNotFound", 404));

        return await ch.Migrate();
    }
}
