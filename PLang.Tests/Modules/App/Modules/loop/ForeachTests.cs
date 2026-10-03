using app.actor.context;
using app;
using app.type.item.variable;

namespace PLang.Tests.App.actions.loop;

public class ForeachTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
    }

    [Test]
    public async Task Foreach_OrchestatesGoalCall()
    {
        var context = _app.actor.list.User.Context;
        var items = new List<object?> { "a", "b", "c" };
        context.Variable.Set("items", items);

        _app.goal.list.Add(new Goal { Name = "ProcessItem", Path = global::app.type.item.path.@this.Resolve("/ProcessItem.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "ForeachRunner",
            Make.Step("foreach %items%, call ProcessItem item=%item%",
                Make.Action(context, "loop", "foreach",
                    Make.Template(context, "collection", "%items%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "ProcessItem")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        // the body ran for each element (its write reaches the caller); the loop's own %item% ends with it
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("seen"))).IsEqualTo("c");
        await Assert.That((await context.Variable.Get("item")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task Foreach_EmptyCollection_ReturnsZeroCount()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("items", new List<object?>());

        var action = global::PLang.Tests.Shared.Make.Action(context, "loop", "foreach",
            ("collection", "%items%"), global::PLang.Tests.Shared.Make.Param(context, "Item", "%item%", "variable"));
        var result = await action.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["count"]!).IsEqualTo(0L);
        await Assert.That((bool)loopResult["complete"]!).IsTrue();
    }

    [Test]
    public async Task Foreach_SetsItemVariable()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("items", new List<object?> { "hello" });

        _app.goal.list.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "SetsItemRunner",
            Make.Step("foreach %items%, call DoNothing item=%myItem%",
                Make.Action(context, "loop", "foreach",
                    Make.Template(context, "collection", "%items%"), Make.Param(context, "item", "%myItem%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "DoNothing")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%myItem%", "variable")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        // the named item was the element inside the body; it ends with the loop
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("seen"))).IsEqualTo("hello");
        await Assert.That((await context.Variable.Get("myItem")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task Foreach_IteratesDictionary()
    {
        var context = _app.actor.list.User.Context;
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        context.Variable.Set("dict", dict);

        _app.goal.list.Add(new Goal { Name = "DictGoal", Path = global::app.type.item.path.@this.Resolve("/DictGoal.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "DictRunner",
            Make.Step("foreach %dict%, call DictGoal item=%val%",
                Make.Action(context, "loop", "foreach",
                    Make.Template(context, "collection", "%dict%"), Make.Param(context, "item", "%val%", "variable"), Make.Param(context, "key", "%key%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "DictGoal")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsSuccess();
    }

    [Test]
    public async Task Foreach_Dictionary_KeyIsStringNotIndex()
    {
        var context = _app.actor.list.User.Context;
        // Use single-entry dict so final state = only iteration
        var dict = new Dictionary<string, object?> { ["greeting"] = "hello" };
        context.Variable.Set("dict", dict);

        _app.goal.list.Add(new Goal { Name = "Noop", Path = global::app.type.item.path.@this.Resolve("/Noop.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "DictKeyRunner",
            Make.Step("foreach %dict%, call Noop",
                Make.Action(context, "loop", "foreach",
                    Make.Template(context, "collection", "%dict%"), Make.Param(context, "item", "%val%", "variable"), Make.Param(context, "key", "%key%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "Noop")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seenKey", "variable"), Make.Param(context, "Value", "%key%", "variable")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seenVal", "variable"), Make.Param(context, "Value", "%val%", "variable")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsSuccess();
        // inside the body %key% is the dictionary key (string "greeting"), not the numeric index (0)
        await Assert.That(await context.Variable.GetValue("seenKey")).IsEqualTo("greeting");
        // and %val% is the value ("hello"), not a KeyValuePair struct
        await Assert.That(await context.Variable.GetValue("seenVal")).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_NullCollection_ReturnsZeroCount()
    {
        var context = _app.actor.list.User.Context;

        var action = global::PLang.Tests.Shared.Make.Action(context, "loop", "foreach",
            ("collection", null), global::PLang.Tests.Shared.Make.Param(context, "Item", "%item%", "variable"));
        var result = await action.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["count"]!).IsEqualTo(0L);
        await Assert.That((bool)loopResult["complete"]!).IsTrue();
    }

    [Test]
    public async Task Foreach_Cancellation_StopsIteration()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c", "d", "e" });

        var cts = new CancellationTokenSource();
        context.PushCancellation(cts);
        cts.Cancel();

        var action = global::PLang.Tests.Shared.Make.Action(context, "loop", "foreach",
            ("collection", "%items%"), global::PLang.Tests.Shared.Make.Param(context, "Item", "%item%", "variable"));
        var result = await action.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((bool)loopResult!["complete"]!).IsFalse();
        await Assert.That((long)loopResult["count"]!).IsEqualTo(0L);
    }

    // Replicates the builder's `foreach %plan.steps%` — %plan% is a Data holding a
    // clr(json) plan {description, steps:[...]}. Navigating %plan.steps% must yield the
    // steps array, and each %planStep% must be a step object (has .index), NOT the plan
    // or a goal. This is the fast stand-in for the builder's BuildGoal/Start loop.
    [Test]
    public async Task Foreach_ClrJsonPlanSteps_BindsStepWithIndex()
    {
        var context = _app.actor.list.User.Context;
        const string planJson =
            "{\"description\":\"d\",\"steps\":[{\"index\":0,\"actions\":[\"a\"]},{\"index\":1,\"actions\":[\"b\"]}]}";
        // Born the way llm.query's answer is: the json kind decodes it.
        var plan = await context.App.type.list.Kind("json").Decode(System.Text.Encoding.UTF8.GetBytes(planJson), context);
        plan.Name = "plan";
        await context.Variable.Set(plan);

        // The builder writes child keys onto %plan% between llm.query and the foreach
        // (set %plan.system% = ..., etc.). Replicate one such write onto the clr(json).
        var setChild = global::PLang.Tests.Shared.Make.Action(context, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(context, "Name", "%plan.system%", "variable"), ("value", "sys-prompt"));
        await (await setChild.Start(context)).IsSuccess();

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "PlanStepRunner",
            Make.Step("foreach %plan.steps% item=%planStep%, set %seen% = %planStep%",
                Make.Action(context, "loop", "foreach",
                    Make.Template(context, "collection", "%plan.steps%"), Make.Param(context, "item", "%planStep%", "variable")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%planStep%", "variable")))));
        var result = await goal.Step[0].Start(context);

        await result.IsSuccess();
        var seen = await context.Variable.Get("seen");   // last step (index 1)
        var idx = await (await seen.Get("index")).Value();
        await Assert.That(idx?.ToString()).IsEqualTo("1");
    }

    // a set with its own value runs for each item; the write to at the step's end keeps the loop's answer
    [Test]
    public async Task Foreach_ASetOfItsOwnValue_RunsPerItem_AndTheWriteToKeepsTheLoopsAnswer()
    {
        var context = _app.actor.list.User.Context;
        await context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "PerItemRunner",
            Make.Step("foreach %items%, set %seen% = %item%, write to %r%",
                Make.Action(context, "loop", "foreach",
                    Make.Template(context, "collection", "%items%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "r", "variable"), ("Value", "%!data%")))));

        await (await goal.Step[0].Start(context)).IsSuccess();

        await Assert.That(await context.Variable.GetValue("seen")).IsEqualTo("c");
        var result = (global::app.type.item.dict.@this)(await (await context.Variable.Get("r")).Value())!;
        await Assert.That(result.Get("count", context)!.Peek()!.ToString()).IsEqualTo("3");
        await Assert.That(result.Get("complete", context)!.Peek()!.ToString()).IsEqualTo("true");
    }
}
