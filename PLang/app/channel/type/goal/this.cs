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
public class @this : global::app.channel.type.session.@this
{
    /// <summary>The call this channel runs for each write — a <c>goal.call</c> action.</summary>
    public global::app.goal.step.action.@this Call { get; }

    /// <summary>The argument the written value reaches the goal as: <c>%message%</c>.</summary>
    public const string MessageName = "message";

    private readonly AsyncLocal<bool> _executing = new();

    /// <summary>
    /// True while this channel's goal body is running on the current async context.
    /// </summary>
    public bool IsExecuting => _executing.Value;

    /// <summary>Not while its own goal body runs on this async context: a body that writes to its own name
    /// finds no channel there instead of looping back into itself.</summary>
    public override bool Available => !IsExecuting;

    /// <summary>A channel named <paramref name="name"/> that runs <paramref name="call"/> for <paramref name="actor"/>.
    /// A setting that isn't given keeps the channel's own default. The direction is the one named
    /// (<c>input</c>, <c>output</c>, <c>bidirectional</c> / <c>both</c>); unnamed, a channel called
    /// <c>input</c> or <c>output</c> is that way, and any other both ways (a goal channel can answer an ask).</summary>
    public @this(string name, global::app.goal.step.action.@this call, global::app.actor.@this actor,
        string? direction = null, long? buffer = null, TimeSpan? timeout = null, string? mime = null,
        string? encoding = null, string? encryption = null, string? signing = null)
    {
        Name = name;
        Call = call;
        Actor = actor;
        Direction = direction?.ToLowerInvariant() switch
        {
            "input" => ChannelDirection.Input,
            "output" => ChannelDirection.Output,
            not null => ChannelDirection.Bidirectional,
            null when string.Equals(name, list.@this.Input, StringComparison.OrdinalIgnoreCase) => ChannelDirection.Input,
            null when string.Equals(name, list.@this.Output, StringComparison.OrdinalIgnoreCase) => ChannelDirection.Output,
            null => ChannelDirection.Bidirectional,
        };
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

    public override async Task<global::app.data.@this> Ask(module.action.output.ask action, CancellationToken ct = default)
    {
        var prompt = action.Context.Ok(action.Question == null ? null : await action.Question.Value());
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
        // arguments, a variable of that name where the goal runs. (%!data% is the action before's
        // result; the call's own run replaces it before the goal's first step reads it.)
        await context.Variable.Set(MessageName, new data.@this(MessageName, data.Peek(), data.Type, context: context));

        var prev = _executing.Value;
        _executing.Value = true;
        try
        {
            return await Call.Start(context);
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
