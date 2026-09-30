using app.error;

namespace app.channel.type.goal;

/// <summary>
/// Concrete goal-backed channel. It holds a <c>goal.call</c> action; WriteAsync runs that call with
/// the written Data as its argument (<c>%message%</c> inside the goal) — the call runs as itself,
/// so its own arguments and modifiers apply. Returns the call's result Data.
///
/// Recursion rule: while the goal body is running on the current async context, the channel is not
/// <see cref="Available"/> and the registry's <c>Get</c> finds no channel under its name. A body like <c>- write out %message%</c> on a channel
/// named <c>"output"</c> can't loop back into itself; sibling and late-registered
/// channels stay visible.
/// </summary>
public class @this : global::app.channel.type.session.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A goal channel is born from its settings — a dict of them by name (what channel.set was given).</summary>
    public static bool Takes(global::app.type.@this other) => other.Is("dict");

    /// <summary>The goal channel <paramref name="raw"/>'s settings describe, read by this channel's own property names,
    /// for the actor whose context it is born in: Name and Goal (a <c>goal.call</c>) are required; a setting not
    /// there keeps the channel's own default. Anything else declines; settings missing a required one decline
    /// naming it (<c>GoalChannelIncomplete</c>).</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this self) return self;
        var ctx = data.Context;
        if (raw is not global::app.type.item.dict.@this settings || ctx == null) return null;
        global::app.type.item.@this? Setting(string name) => settings.Get(name, ctx)?.Peek() is { IsNull: false } v ? v : null;
        if (Setting(nameof(Name))?.ToString() is not { Length: > 0 } name)
            return Incomplete($"a goal channel needs its {nameof(Name)}");
        if (Setting(nameof(Goal)) is not global::app.goal.step.action.@this goal)
            return Incomplete($"goal channel '{name}' needs its {nameof(Goal)}: the goal.call each write runs");
        // Direction, buffer and mime are plang values the channel holds as they are; the rest are lowered here
        // to what the transport uses.
        return new @this(name, goal, ctx.Actor,
            direction: Setting(nameof(Direction)) as global::app.type.item.choice.@this<ChannelDirection>,
            buffer: Setting(nameof(Buffer)) as global::app.type.item.number.@this,
            timeout: Setting(nameof(Timeout)) is global::app.type.item.duration.@this after ? (TimeSpan)after : null,
            mime: Setting(nameof(Mime)) as global::app.type.item.text.@this,
            encoding: Setting(nameof(Encoding))?.ToString(),
            encryption: (Setting(nameof(Encryption)) as global::app.type.item.variable.@this)?.Name,
            signing: (Setting(nameof(Signing)) as global::app.type.item.variable.@this)?.Name);

        @this? Incomplete(string why)
        {
            data.Fail(new global::app.error.Error(why, "GoalChannelIncomplete", 400));
            return null;
        }
    }

    /// <summary>The goal this channel runs for each write — a <c>goal.call</c> action.</summary>
    public global::app.goal.step.action.@this Goal { get; }

    /// <summary>The argument the written value reaches the goal as: <c>%message%</c>.</summary>
    private const string MessageName = "message";

    private readonly AsyncLocal<bool> _executing = new();

    /// <summary>
    /// True while this channel's goal body is running on the current async context.
    /// </summary>
    public bool IsExecuting => _executing.Value;

    /// <summary>Not while its own goal body runs on this async context: a body that writes to its own name
    /// finds no channel there instead of looping back into itself.</summary>
    public override bool Available => !IsExecuting;

    /// <summary>A channel named <paramref name="name"/> that runs <paramref name="goal"/> for <paramref name="actor"/>.
    /// A setting that isn't given keeps the channel's own default. Without a direction, a channel called
    /// <c>input</c> or <c>output</c> is that way, and any other both ways (a goal channel can answer an ask).</summary>
    protected @this(string name, global::app.goal.step.action.@this goal, global::app.actor.@this actor,
        global::app.type.item.choice.@this<ChannelDirection>? direction = null, global::app.type.item.number.@this? buffer = null,
        TimeSpan? timeout = null, global::app.type.item.text.@this? mime = null,
        string? encoding = null, string? encryption = null, string? signing = null)
    {
        Name = name;
        Goal = goal;
        Actor = actor;
        Direction = direction
            ?? (string.Equals(name, list.@this.Input, StringComparison.OrdinalIgnoreCase) ? ChannelDirection.Input
              : string.Equals(name, list.@this.Output, StringComparison.OrdinalIgnoreCase) ? ChannelDirection.Output
              : ChannelDirection.Bidirectional);
        if (buffer is { } b) Buffer = b;
        if (timeout is { } t) Timeout = t;
        if (mime is { } m) Mime = m;
        if (encoding is { } e) Encoding = e;
        if (encryption is { } key) Encryption = key;
        if (signing is { } s) Signing = s;
    }

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        return await InvokeGoal(data, ct);
    }

    public override async Task<global::app.data.@this> Read(CancellationToken ct = default)
    {
        return await InvokeGoal(Actor.Context.Ok((object?)null), ct);
    }

    public override async Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
    {
        var prompt = action.Context.Ok(action.Question == null ? null : await action.Question.Value());
        // The goal's result is the answer, as it came.
        return await InvokeGoal(prompt, ct);
    }

    private async Task<global::app.data.@this> InvokeGoal(global::app.data.@this data, CancellationToken ct)
    {
        if (!IsOpen)
            return data.Context.Error(new ServiceError($"Channel '{Name}' is closed", "ChannelClosed", 400));

        var context = Actor.Context;

        // Channels are not a fork — `write out %x%` is just a function call from
        // the user's POV, and the channel layer is the plumbing under it. Whatever
        // upstream operator forked the flow (parallel foreach iteration, async
        // call, listener accept-loop, etc.) has already pushed a Calls overlay,
        // and AsyncLocal carries it down to here.
        // The written value is the call's argument %message% — bound as goal.call binds its own
        // arguments, in a frame that ends with the run. (%!data% is the action before's
        // result; the call's own run replaces it before the goal's first step reads it.)
        var message = new data.@this(MessageName, data.Peek(), data.Type, context: context);

        var prev = _executing.Value;
        _executing.Value = true;
        try
        {
            await using (context.Variable.Calls.Push([message])) return await Goal.Start(context);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return context.Error(new ServiceError(
                $"Goal channel '{Name}' failed: {ex.Message}", "GoalChannelError") { Exception = ex });
        }
        finally
        {
            _executing.Value = prev;
        }
    }

    public override void Close()
    {
        // Goals are app-owned; closing the channel disposes nothing it runs.
        IsOpen = false;
    }

    public override ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
