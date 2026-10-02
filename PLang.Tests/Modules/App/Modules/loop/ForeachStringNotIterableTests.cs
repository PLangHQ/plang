using app.actor.context;
using app;
using app.type.item.variable;

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
    public void Setup() => _app = new global::app.@this("/app").Testing();

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The headline test: Collection="hello" runs the body exactly ONCE. Without
    // the carve-out it would be 5.
    [Test]
    public async Task Foreach_StringCollection_RunsBodyExactlyOnce()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("s", "hello");

        // Body goal runs once per iteration.
        _app.goal.list.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "StringRunner",
            Make.Step("foreach %s%, call DoNothing",
                Make.Action(context, "loop", "foreach",
                    ("collection", "%s%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "DoNothing")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["count"]!).IsEqualTo(1L);
    }

    // Body sees the WHOLE string in %item%, not the first char.
    [Test]
    public async Task Foreach_StringCollection_BodyReceivesWholeString()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("s", "hello");

        _app.goal.list.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "WholeStringRunner",
            Make.Step("foreach %s%, call DoNothing",
                Make.Action(context, "loop", "foreach",
                    ("collection", "%s%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "DoNothing")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")))));
        var step = goal.Step[0];

        await step.Start(context);

        await Assert.That((await context.Variable.GetValue("seen"))).IsEqualTo("hello");
    }

    // Same single-iteration shape for non-iterable scalars in general.
    [Test]
    public async Task Foreach_NumberCollection_RunsBodyOnceWithNumber()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("n", 42);

        _app.goal.list.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "NumberRunner",
            Make.Step("foreach %n%, call DoNothing",
                Make.Action(context, "loop", "foreach",
                    ("collection", "%n%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "DoNothing")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["count"]!).IsEqualTo(1L);
        await Assert.That((await context.Variable.GetValue("seen"))).IsEqualTo(42);
    }
}
