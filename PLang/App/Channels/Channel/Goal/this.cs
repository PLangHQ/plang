using App.Errors;

namespace App.Channels.Channel.Goal;

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
public class @this : Session.@this
{
    /// <summary>The goal this channel dispatches writes to.</summary>
    public global::App.Goals.Goal.@this Goal { get; }

    public @this(string name, global::App.Goals.Goal.@this goal, global::App.Actor.@this actor,
        ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        Name = name;
        Goal = goal;
        Actor = actor;
        Direction = direction;
    }

    public override async Task<Data.@this> WriteCore(Data.@this data, CancellationToken ct = default)
    {
        return await InvokeGoal(data, ct);
    }

    public override async Task<Data.@this> ReadCore(CancellationToken ct = default)
    {
        return await InvokeGoal(Data.@this.Ok((object?)null), ct);
    }

    public override async Task<Data.@this> AskCore(modules.output.ask action, CancellationToken ct = default)
    {
        var prompt = global::App.Data.@this.Ok(action.Question?.Value);
        return await InvokeGoal(prompt, ct);
    }

    private async Task<Data.@this> InvokeGoal(Data.@this data, CancellationToken ct)
    {
        if (!IsOpen)
            return Data.@this.FromError(new ServiceError($"Channel '{Name}' is closed", "ChannelClosed", 400));

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
        ctx.Variables.Set("!data", new Data.@this("!data", data.Value, data.Type));

        try
        {
            return await app.RunGoalAsync(Goal, ctx, ct);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return Data.@this.FromError(new ServiceError(
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
