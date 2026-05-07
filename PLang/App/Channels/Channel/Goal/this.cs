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

    /// <summary>The actor that registered this channel — its FoundationalChannels are used during goal execution.</summary>
    public global::App.Actor.@this RegisteringActor { get; }

    public @this(string name, global::App.Goals.Goal.@this goal, global::App.Actor.@this registeringActor,
        ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        Name = name;
        Goal = goal;
        RegisteringActor = registeringActor;
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

    public override async Task<Data.@this> AskCore(Data.@this prompt, CancellationToken ct = default)
    {
        return await InvokeGoal(prompt, ct);
    }

    private async Task<Data.@this> InvokeGoal(Data.@this data, CancellationToken ct)
    {
        if (!IsOpen)
            return Data.@this.FromError(new ServiceError($"Channel '{Name}' is closed", "ChannelClosed", 400));

        var app = RegisteringActor.App;
        var foundational = RegisteringActor.FoundationalChannels;
        // Switch the actor's channel resolution to the foundational set for the duration
        // of the goal call. AsyncLocal scoping means concurrent calls don't collide.
        using var _ = RegisteringActor.PushChannelsOverride(foundational);

        // Bind the inbound Data as %!data% so the goal body can reference it.
        var ctx = RegisteringActor.Context;
        ctx.Variables.Set("!data", data);

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

    /// <summary>
    /// Goal channels migrate by carrying the goal name (resolveable on the
    /// receiver) and a Variables snapshot. Stage 9 stub — full transport ships
    /// when the receive-side runtime lands.
    /// </summary>
    public override Task<Data.@this> Migrate()
    {
        var payload = new GoalMigrationPayload
        {
            GoalName = Goal.Name ?? "",
            Variables = RegisteringActor.Context.Variables.Snapshot()
        };
        var envelope = new global::App.Channels.Channel.MigrationEnvelope
        {
            Name = Name,
            Direction = Direction,
            Config = SnapshotConfig(),
            Payload = payload,
            Signature = SignEmpty()
        };
        return Task.FromResult(Data.@this.Ok(envelope));
    }
}

/// <summary>Goal-channel migration payload — goal name + Variables snapshot.</summary>
public sealed class GoalMigrationPayload
{
    public required string GoalName { get; init; }
    public required object Variables { get; init; }
}
