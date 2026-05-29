using app.error;

namespace app.channel.goal;

/// <summary>
/// Concrete goal-backed channel. WriteAsync invokes the wrapped goal with the Data
/// Data as input (available as <c>%!data%</c> inside the goal). Returns the
/// goal's result Data.
///
/// Recursion rule: while the goal body is running on the current async context,
/// <see cref="IsExecuting"/> is true and the registry's <c>Get</c> treats this
/// channel as not-found. A body like <c>- write out %!data%</c> on a channel
/// named <c>"output"</c> can't loop back into itself; sibling and late-registered
/// channels stay visible.
/// </summary>
public class @this : global::app.channel.session.@this
{
    /// <summary>The goal this channel dispatches writes to.</summary>
    public global::app.goal.@this Goal { get; }

    private readonly AsyncLocal<bool> _executing = new();

    /// <summary>
    /// True while this channel's goal body is running on the current async context.
    /// The registry's <c>Get</c> treats an executing goal-channel as not-found, so
    /// a body that writes to its own name surfaces <c>ChannelNotFound</c> instead
    /// of looping back into itself.
    /// </summary>
    public bool IsExecuting => _executing.Value;

    public @this(string name, global::app.goal.@this goal, global::app.actor.@this actor,
        ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        Name = name;
        Goal = goal;
        Actor = actor;
        Direction = direction;
    }

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        return await InvokeGoal(data, ct);
    }

    public override async Task<global::app.data.@this> Read(CancellationToken ct = default)
    {
        return await InvokeGoal(global::app.data.@this.Ok((object?)null), ct);
    }

    public override async Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
    {
        var prompt = global::app.data.@this.Ok(action.Question?.Value);
        return await InvokeGoal(prompt, ct);
    }

    private async Task<global::app.data.@this> InvokeGoal(global::app.data.@this data, CancellationToken ct)
    {
        if (!IsOpen)
            return global::app.data.@this.FromError(new ServiceError($"Channel '{Name}' is closed", "ChannelClosed", 400));

        var context = Actor.Context;

        // Channels are not a fork — `write out %x%` is just a function call from
        // the user's POV, and the channel layer is the plumbing under it. Whatever
        // upstream operator forked the flow (parallel foreach iteration, async
        // call, listener accept-loop, etc.) has already pushed a Calls overlay,
        // and AsyncLocal carries it down to here. Variables.Set("!data", ...)
        // lands in that overlay if there is one, in the actor-shared dict
        // otherwise — and either way subsequent goal-body sets behave the same.
        context.Variables.Set("!data", new data.@this("!data", data.Value, data.Type));

        var prev = _executing.Value;
        _executing.Value = true;
        try
        {
            return await Actor.App.RunGoalAsync(Goal, context, ct);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Goal channel '{Name}' failed: {ex.Message}", "GoalChannelError") { Exception = ex });
        }
        finally
        {
            _executing.Value = prev;
        }
    }

    public override void Close()
    {
        // Goals are app-owned; we don't dispose the wrapped Goal.
        IsOpen = false;
    }

    public override ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
