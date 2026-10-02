namespace app.module.channel;

/// <summary>
/// Unregisters a channel by name, on the actor named (else the asker's). A default channel
/// (output/error/input) can't be removed — the boot invariant needs it; <c>channel.set</c> replaces its backing.
/// PLang surface:
///   - remove channel "logger"
/// </summary>
[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }
    public partial data.@this<global::app.type.item.choice.@this<global::app.actor.Name>>? Actor { get; init; }

    public Task<data.@this> Start() => Name.Use(name =>
        Context.App.actor.list.Use(Actor, Context, runs => runs.Actor.Channel.Remove(name.ToString(), Context)));
}
