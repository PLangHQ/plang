using global::app.actor.context;
using app;
using global::app.Variables;

namespace PLang.Tests.App.Foundation;

/// <summary>
/// Proves Fix #5: EventsFor() caches results, but registering new events at runtime
/// does NOT invalidate the cache — subsequent calls return stale data.
/// These tests assert CORRECT behavior (cache invalidation on registration).
/// Before the fix they FAIL, proving the bug. After the fix they PASS.
/// </summary>
public class EventCacheInvalidationTests
{
    [Test]
    public async Task EventsFor_Goal_PicksUpNewlyRegisteredEvent()
    {
        await using var engine = new global::app.@this("/app");
        using var context = new global::app.actor.context.@this(engine);
        var goal = new Goal { Name = "TestGoal", Path = "\\TestGoal.goal" };

        // Register first event
        context.Events.Register(new EventBinding(
            EventType.BeforeGoal,
            async (ctx, _, _) => Data.Ok(),
            goalNamePattern: "TestGoal"));

        // Resolve events — this gets cached
        var events1 = context.LifecycleFor(goal);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register second event at runtime
        context.Events.Register(new EventBinding(
            EventType.BeforeGoal,
            async (ctx, _, _) => Data.Ok(),
            goalNamePattern: "TestGoal"));

        // Resolve again — should see 2 events, not stale cached 1
        var events2 = context.LifecycleFor(goal);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsFor_Step_PicksUpNewlyRegisteredEvent()
    {
        await using var engine = new global::app.@this("/app");
        using var context = new global::app.actor.context.@this(engine);
        var goal = new Goal { Name = "TestGoal", Path = "\\TestGoal.goal" };
        var step = new Step { Text = "do something" };
        step.Goal = goal;

        // Register first step event
        context.Events.Register(new EventBinding(
            EventType.BeforeStep,
            async (ctx, _, _) => Data.Ok(),
            goalNamePattern: "TestGoal",
            stepPattern: "do something"));

        // Resolve — cached
        var events1 = context.LifecycleFor(step);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register another step event at runtime
        context.Events.Register(new EventBinding(
            EventType.BeforeStep,
            async (ctx, _, _) => Data.Ok(),
            goalNamePattern: "TestGoal",
            stepPattern: "do something"));

        // Should see 2, not stale 1
        var events2 = context.LifecycleFor(step);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsFor_Action_PicksUpNewlyRegisteredEvent()
    {
        await using var engine = new global::app.@this("/app");
        using var context = new global::app.actor.context.@this(engine);
        var action = new global::app.goals.goal.steps.step.actions.action.@this
        {
            Module = "variable",
            ActionName = "set"
        };

        // Register first action event
        context.Events.Register(new EventBinding(
            EventType.BeforeAction,
            async (ctx, _, _) => Data.Ok(),
            actionPattern: "variable.set"));

        // Resolve — cached
        var events1 = context.LifecycleFor(action);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register another action event at runtime
        context.Events.Register(new EventBinding(
            EventType.BeforeAction,
            async (ctx, _, _) => Data.Ok(),
            actionPattern: "variable.set"));

        // Should see 2, not stale 1
        var events2 = context.LifecycleFor(action);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsFor_ManualInvalidation_Works()
    {
        await using var engine = new global::app.@this("/app");
        using var context = new global::app.actor.context.@this(engine);
        var goal = new Goal { Name = "TestGoal", Path = "\\TestGoal.goal" };

        // Register and cache
        context.Events.Register(new EventBinding(
            EventType.BeforeGoal,
            async (ctx, _, _) => Data.Ok(),
            goalNamePattern: "TestGoal"));
        var events1 = context.LifecycleFor(goal);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register new event + manually invalidate cache
        context.Events.Register(new EventBinding(
            EventType.BeforeGoal,
            async (ctx, _, _) => Data.Ok(),
            goalNamePattern: "TestGoal"));
        context.InvalidateEventCache();

        // Now should pick up the new event
        var events2 = context.LifecycleFor(goal);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }
}
