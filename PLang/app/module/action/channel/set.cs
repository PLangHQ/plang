using app.error;

namespace app.module.action.channel;

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
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }
    /// <summary>The call that backs the channel — a <c>goal.call</c> action, run as itself (its own
    /// arguments and modifiers) for each message.</summary>
    public partial data.@this<global::app.goal.step.action.@this> Goal { get; init; }
    public partial data.@this<global::app.type.item.choice.@this<global::app.actor.Name>>? Actor { get; init; }
    public partial data.@this<global::app.type.item.number.@this>? Buffer { get; init; }
    public partial data.@this<global::app.type.item.duration.@this>? Timeout { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Mime { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Encoding { get; init; }
    /// <summary>"input", "output", or "bidirectional". Unnamed, the channel decides (a channel called
    /// "input" or "output" is that way).</summary>
    public partial data.@this<global::app.type.item.text.@this>? Direction { get; init; }
    public partial data.@this<app.type.item.variable.@this>? Encryption { get; init; }
    public partial data.@this<app.type.item.variable.@this>? Signing { get; init; }

    // The channel owns its defaults and its direction: only what the step gives is handed over.
    public Task<data.@this> Start() => Name.Use(name => Goal.Use(async call =>
    {
        var named = Actor == null ? null : await Actor.Value();
        var actor = named == null ? Context.Actor : (await (await Context.App.actor.Get(named.ToString()!)).Value())!;
        var ch = new app.channel.type.goal.@this(name.ToString(), call, actor,
            direction: Direction == null ? null : (await Direction.Value())?.ToString(),
            buffer: Buffer == null ? null : (await Buffer.Value())?.ToInt64(),
            timeout: Timeout == null ? null : (await Timeout.Value()) is { } to ? (TimeSpan)to : null,
            mime: Mime == null ? null : (await Mime.Value())?.ToString(),
            encoding: Encoding == null ? null : (await Encoding.Value())?.ToString(),
            encryption: Encryption == null ? null : (await Encryption.Value())?.Name,
            signing: Signing == null ? null : (await Signing.Value())?.Name);

        // Upsert: dispose any existing channel under this name before re-registering.
        await actor.Channel.RemoveAsync(ch.Name);
        actor.Channel.Register(ch);
        return (data.@this)Context.Ok(ch);
    }));
}
