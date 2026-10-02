namespace app.module.channel;

/// <summary>
/// Registers or replaces a named channel backed by a goal call. Upserts — if a
/// channel with the same name already exists, it is disposed and replaced.
/// PLang surface:
///   - set output channel as MyOutputGoal
///   - set channel "logger" call Logger
///   - set channel "audit" call AuditLog, buffer: 65536, timeout: PT30S
///   - set system input channel as InputGoal
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }
    /// <summary>The call that backs the channel — a <c>goal.call</c> action, run as itself (its own
    /// arguments and modifiers) for each message.</summary>
    [IsNotNull]
    public partial data.@this<global::app.goal.step.action.@this> Goal { get; init; }
    public partial data.@this<global::app.type.item.choice.@this<global::app.actor.Name>>? Actor { get; init; }
    public partial data.@this<global::app.type.item.number.@this>? Buffer { get; init; }
    public partial data.@this<global::app.type.item.duration.@this>? Timeout { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Mime { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Encoding { get; init; }
    /// <summary>Input, Output or Bidirectional. Unnamed, the channel decides (a channel called "input" or
    /// "output" is that way).</summary>
    public partial data.@this<global::app.type.item.choice.@this<global::app.channel.ChannelDirection>>? Direction { get; init; }
    public partial data.@this<app.type.item.variable.@this>? Encryption { get; init; }
    public partial data.@this<app.type.item.variable.@this>? Signing { get; init; }

    // The channel is born from what the step gave, through its type, for the actor named (else the asker);
    // it replaces one of the same name.
    public async Task<data.@this> Start()
    {
        var given = await Given();
        if (!given.Success) return given;
        return await Context.App.actor.list.Use(Actor, Context, async runs =>
        {
            var born = await Context.App.type.list[typeof(app.channel.type.goal.@this)].Create(given.Peek(), runs);
            return await born.Use<app.channel.type.goal.@this>(async channel =>
            {
                await channel.Actor.Channel.Set(channel);
                return born;
            });
        });
    }
}
