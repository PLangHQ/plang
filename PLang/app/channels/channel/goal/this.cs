using app.errors;

namespace app.channels.channel.goal;

/// <summary>
/// Concrete goal-backed channel. WriteAsync invokes the wrapped goal with the Data
/// envelope as input (available as <c>%!data%</c> inside the goal). Returns the
/// goal's result Data.
///
/// Recursion rule: the channel captures a reference to the registering actor, and
/// for the duration of each goal call swaps the actor's channel resolution to the
/// actor's <see cref="Actor.@this.FoundationalChannels"/>. So a goal-channel body
/// like <c>- write out %!data%</c> reaches the original entry-point streams, not
/// the overlay that fired this goal — preventing infinite recursion and giving
/// fan-out via composition for free.
/// </summary>
public class @this : global::app.channels.channel.session.@this
{
    /// <summary>The goal this channel dispatches writes to.</summary>
    public global::app.goals.goal.@this Goal { get; }

    public @this(string name, global::app.goals.goal.@this goal, global::app.actor.@this actor,
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

    public override async Task<global::app.data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
    {
        var prompt = global::app.data.@this.Ok(action.Question?.Value);
        return await InvokeGoal(prompt, ct);
    }

    private async Task<global::app.data.@this> InvokeGoal(global::app.data.@this data, CancellationToken ct)
    {
        if (!IsOpen)
            return global::app.data.@this.FromError(new ServiceError($"Channel '{Name}' is closed", "ChannelClosed", 400));

        var app = Actor.App;
        var foundational = Actor.FoundationalChannels;
        // Switch the actor's channel resolution to the foundational set for the duration
        // of the goal call. AsyncLocal scoping means concurrent calls don't collide.
        using var _ = Actor.PushChannelsOverride(foundational);

        var ctx = Actor.Context;

        // Channels are not a fork — `write out %x%` is just a function call from
        // the user's POV, and the channel layer is the plumbing under it. Whatever
        // upstream operator forked the flow (parallel foreach iteration, async
        // call, listener accept-loop, etc.) has already pushed a Calls overlay,
        // and AsyncLocal carries it down to here. Variables.Set("!data", ...)
        // lands in that overlay if there is one, in the actor-shared dict
        // otherwise — and either way subsequent goal-body sets behave the same.
        ctx.Variables.Set("!data", new data.@this("!data", data.Value, data.Type));

        try
        {
            return await app.RunGoalAsync(Goal, ctx, ct);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Goal channel '{Name}' failed: {ex.Message}", "GoalChannelError") { Exception = ex });
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
