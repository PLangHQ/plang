using app.actor.context;
using app;
using app.variable;

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
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The headline test: Collection="hello" runs the body exactly ONCE. Without
    // the carve-out it would be 5.
    [Test]
    public async Task Foreach_StringCollection_RunsBodyExactlyOnce()
    {
        var context = _app.User.Context;
        context.Variable.Set("s", "hello");

        // Body goal runs once per iteration.
        _app.Goal.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("StringRunner",
            Make.Step("foreach %s%, call DoNothing",
                Make.Action("loop", "foreach",
                    ("collection", "%s%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["itemCount"]!).IsEqualTo(1L);
    }

    // Body sees the WHOLE string in %item%, not the first char.
    [Test]
    public async Task Foreach_StringCollection_BodyReceivesWholeString()
    {
        var context = _app.User.Context;
        context.Variable.Set("s", "hello");

        _app.Goal.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("WholeStringRunner",
            Make.Step("foreach %s%, call DoNothing",
                Make.Action("loop", "foreach",
                    ("collection", "%s%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" })))));
        var step = goal.Step.list.First();

        await step.Run(context);

        await Assert.That((await context.Variable.GetValue("item"))).IsEqualTo("hello");
    }

    // Same single-iteration shape for non-iterable scalars in general.
    [Test]
    public async Task Foreach_NumberCollection_RunsBodyOnceWithNumber()
    {
        var context = _app.User.Context;
        context.Variable.Set("n", 42);

        _app.Goal.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("NumberRunner",
            Make.Step("foreach %n%, call DoNothing",
                Make.Action("loop", "foreach",
                    ("collection", "%n%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["itemCount"]!).IsEqualTo(1L);
        await Assert.That((await context.Variable.GetValue("item"))).IsEqualTo(42);
    }
}
