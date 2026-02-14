using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Tests.Runtime2.Foundation;

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
        await using var engine = new Engine("/app");
        using var context = new PLangContext(engine);
        var goal = new Goal { Name = "TestGoal", Path = "\\TestGoal.goal" };

        // Register first event
        context.User.Events.Register(
            EventType.BeforeGoal,
            async ctx => Data.Ok(),
            goalNamePattern: "TestGoal");

        // Resolve events — this gets cached
        var events1 = context.LifecycleFor(goal);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register second event at runtime
        context.User.Events.Register(
            EventType.BeforeGoal,
            async ctx => Data.Ok(),
            goalNamePattern: "TestGoal");

        // Resolve again — should see 2 events, not stale cached 1
        var events2 = context.LifecycleFor(goal);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsFor_Step_PicksUpNewlyRegisteredEvent()
    {
        await using var engine = new Engine("/app");
        using var context = new PLangContext(engine);
        var goal = new Goal { Name = "TestGoal", Path = "\\TestGoal.goal" };
        var step = new Step { Text = "do something" };
        step.Goal = goal;

        // Register first step event
        context.User.Events.Register(
            EventType.BeforeStep,
            async ctx => Data.Ok(),
            goalNamePattern: "TestGoal",
            stepPattern: "do something");

        // Resolve — cached
        var events1 = context.LifecycleFor(step);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register another step event at runtime
        context.User.Events.Register(
            EventType.BeforeStep,
            async ctx => Data.Ok(),
            goalNamePattern: "TestGoal",
            stepPattern: "do something");

        // Should see 2, not stale 1
        var events2 = context.LifecycleFor(step);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsFor_Action_PicksUpNewlyRegisteredEvent()
    {
        await using var engine = new Engine("/app");
        using var context = new PLangContext(engine);
        var action = new PLang.Runtime2.Core.Action
        {
            Module = "variable",
            ActionName = "set"
        };

        // Register first action event
        context.User.Events.Register(
            EventType.BeforeAction,
            async ctx => Data.Ok(),
            actionPattern: "variable.set");

        // Resolve — cached
        var events1 = context.LifecycleFor(action);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register another action event at runtime
        context.User.Events.Register(
            EventType.BeforeAction,
            async ctx => Data.Ok(),
            actionPattern: "variable.set");

        // Should see 2, not stale 1
        var events2 = context.LifecycleFor(action);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsFor_ManualInvalidation_Works()
    {
        await using var engine = new Engine("/app");
        using var context = new PLangContext(engine);
        var goal = new Goal { Name = "TestGoal", Path = "\\TestGoal.goal" };

        // Register and cache
        context.User.Events.Register(
            EventType.BeforeGoal,
            async ctx => Data.Ok(),
            goalNamePattern: "TestGoal");
        var events1 = context.LifecycleFor(goal);
        await Assert.That(events1.Before.Count).IsEqualTo(1);

        // Register new event + manually invalidate cache
        context.User.Events.Register(
            EventType.BeforeGoal,
            async ctx => Data.Ok(),
            goalNamePattern: "TestGoal");
        context.InvalidateEventCache();

        // Now should pick up the new event
        var events2 = context.LifecycleFor(goal);
        await Assert.That(events2.Before.Count).IsEqualTo(2);
    }
}
