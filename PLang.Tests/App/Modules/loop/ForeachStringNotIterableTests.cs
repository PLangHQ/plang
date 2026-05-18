using global::app.actor.context;
using app;
using global::app.variables;
using LoopResult = global::app.modules.loop.types.loop;

namespace PLang.Tests.App.actions.loop;

// Phase 5 + Phase 2c spot-check — foreach over a string runs ONCE, not once
// per char. Strings are atomic in plang. Same predicate (IsPlangIterable)
// used by AsEnumerable and EnumerateItems handles this.
//
// PlangAssignabilityTests covers the predicate and Data.AsEnumerable directly.
// This file covers the consumer-side: the foreach handler's actual loop count.

public class ForeachStringNotIterableTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The headline test: Collection="hello" runs the body exactly ONCE. Without
    // the carve-out it would be 5.
    [Test]
    public async Task Foreach_StringCollection_RunsBodyExactlyOnce()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set("s", "hello");

        // Body goal runs once per iteration.
        var goal = new Goal { Name = "DoNothing", Path = "/DoNothing.goal", Steps = new GoalSteps() };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%s%"), ("itemname", "%item%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" }));
        var step = new Step
        {
            Index = 0,
            Text = "foreach %s%, call DoNothing",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(ctx);

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(1);
    }

    // Body sees the WHOLE string in %item%, not the first char.
    [Test]
    public async Task Foreach_StringCollection_BodyReceivesWholeString()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set("s", "hello");

        var goal = new Goal { Name = "DoNothing", Path = "/DoNothing.goal", Steps = new GoalSteps() };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%s%"), ("itemname", "%item%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" }));
        var step = new Step
        {
            Index = 0,
            Text = "foreach %s%, call DoNothing",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        await step.RunAsync(ctx);

        await Assert.That(ctx.Variables.GetValue("item")).IsEqualTo("hello");
    }

    // Same single-iteration shape for non-iterable scalars in general.
    [Test]
    public async Task Foreach_NumberCollection_RunsBodyOnceWithNumber()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set("n", 42);

        var goal = new Goal { Name = "DoNothing", Path = "/DoNothing.goal", Steps = new GoalSteps() };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%n%"), ("itemname", "%item%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" }));
        var step = new Step
        {
            Index = 0,
            Text = "foreach %n%, call DoNothing",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(ctx);

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(1);
        await Assert.That(ctx.Variables.GetValue("item")).IsEqualTo(42);
    }
}
