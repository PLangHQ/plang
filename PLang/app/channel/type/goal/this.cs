using app.error;

namespace app.channel.type.goal;

/// <summary>
/// Concrete goal-backed channel. It holds a <c>goal.call</c> action; WriteAsync runs that call with
/// the written Data as its argument (<c>%message%</c> inside the goal) — the call runs as itself,
/// so its own arguments and modifiers apply. Returns the call's result Data.
///
/// Recursion rule: while the goal body is running on the current async context,
/// <see cref="IsExecuting"/> is true and the registry's <c>Get</c> treats this
/// channel as not-found. A body like <c>- write out %message%</c> on a channel
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
    /// The registry's <c>Get</c> treats an executing goal-channel as not-found, so
    /// a body that writes to its own name surfaces <c>ChannelNotFound</c> instead
    /// of looping back into itself.
    /// </summary>
    public bool IsExecuting => _executing.Value;

    public @this(string name, global::app.goal.step.action.@this call, global::app.actor.@this actor,
        ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        Name = name;
        Call = call;
        Actor = actor;
        Direction = direction;
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
            return await Call.Run(context);
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
