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
        _app = TestApp.Create("/app");
    }

    [Test]
    public async Task Foreach_OrchestatesGoalCall()
    {
        var context = _app.User.Context;
        var items = new List<object?> { "a", "b", "c" };
        context.Variable.Set("items", items);

        _app.goal.list.Add(new Goal { Name = "ProcessItem", Path = global::app.type.item.path.@this.Resolve("/ProcessItem.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("ForeachRunner",
            Make.Step("foreach %items%, call ProcessItem item=%item%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%items%"), Make.Param("item", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("name", "ProcessItem")),
                Make.Action("variable", "set", Make.Param("Name", "seen", "variable"), Make.Param("Value", "%item%", "variable")))));
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
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?>());

        var action = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("item", "%item%"));
        var result = await action.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["itemCount"]!).IsEqualTo(0L);
        await Assert.That((bool)loopResult["completed"]!).IsTrue();
    }

    [Test]
    public async Task Foreach_SetsItemVariable()
    {
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?> { "hello" });

        _app.goal.list.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("SetsItemRunner",
            Make.Step("foreach %items%, call DoNothing item=%myItem%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%items%"), Make.Param("item", "%myItem%", "variable")),
                Make.Action("goal", "call",
                    ("name", "DoNothing")),
                Make.Action("variable", "set", Make.Param("Name", "seen", "variable"), Make.Param("Value", "%myItem%", "variable")))));
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
        var context = _app.User.Context;
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        context.Variable.Set("dict", dict);

        _app.goal.list.Add(new Goal { Name = "DictGoal", Path = global::app.type.item.path.@this.Resolve("/DictGoal.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("DictRunner",
            Make.Step("foreach %dict%, call DictGoal item=%val%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%dict%"), Make.Param("item", "%val%", "variable"), Make.Param("key", "%key%", "variable")),
                Make.Action("goal", "call",
                    ("name", "DictGoal")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsSuccess();
    }

    [Test]
    public async Task Foreach_Dictionary_KeyIsStringNotIndex()
    {
        var context = _app.User.Context;
        // Use single-entry dict so final state = only iteration
        var dict = new Dictionary<string, object?> { ["greeting"] = "hello" };
        context.Variable.Set("dict", dict);

        _app.goal.list.Add(new Goal { Name = "Noop", Path = global::app.type.item.path.@this.Resolve("/Noop.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("DictKeyRunner",
            Make.Step("foreach %dict%, call Noop",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%dict%"), Make.Param("item", "%val%", "variable"), Make.Param("key", "%key%", "variable")),
                Make.Action("goal", "call",
                    ("name", "Noop")),
                Make.Action("variable", "set", Make.Param("Name", "seenKey", "variable"), Make.Param("Value", "%key%", "variable")),
                Make.Action("variable", "set", Make.Param("Name", "seenVal", "variable"), Make.Param("Value", "%val%", "variable")))));
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
        var context = _app.User.Context;

        var action = TestAction.Create("loop", "foreach",
            ("collection", null), ("item", "%item%"));
        var result = await action.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((long)loopResult!["itemCount"]!).IsEqualTo(0L);
        await Assert.That((bool)loopResult["completed"]!).IsTrue();
    }

    [Test]
    public async Task Foreach_Cancellation_StopsIteration()
    {
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c", "d", "e" });

        var cts = new CancellationTokenSource();
        context.PushCancellation(cts);
        cts.Cancel();

        var action = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("item", "%item%"));
        var result = await action.Start(context);

        await result.IsSuccess();
        var loopResult = Lower<Dictionary<string, object?>>(await result.Value());
        await Assert.That((bool)loopResult!["completed"]!).IsFalse();
        await Assert.That((long)loopResult["itemCount"]!).IsEqualTo(0L);
    }

    // Replicates the builder's `foreach %plan.steps%` — %plan% is a Data holding a
    // clr(json) plan {description, steps:[...]}. Navigating %plan.steps% must yield the
    // steps array, and each %planStep% must be a step object (has .index), NOT the plan
    // or a goal. This is the fast stand-in for the builder's BuildGoal/Start loop.
    [Test]
    public async Task Foreach_ClrJsonPlanSteps_BindsStepWithIndex()
    {
        var context = _app.User.Context;
        const string planJson =
            "{\"description\":\"d\",\"steps\":[{\"index\":0,\"actions\":[\"a\"]},{\"index\":1,\"actions\":[\"b\"]}]}";
        // Born the way llm.query's answer is: the json kind decodes it.
        var plan = await context.App.type.list.Kind("json").Decode(System.Text.Encoding.UTF8.GetBytes(planJson), context);
        plan.Name = "plan";
        await context.Variable.Set(plan);

        // The builder writes child keys onto %plan% between llm.query and the foreach
        // (set %plan.system% = ..., etc.). Replicate one such write onto the clr(json).
        var setChild = TestAction.Create("variable", "set",
            ("name", "%plan.system%"), ("value", "sys-prompt"));
        await (await setChild.Start(context)).IsSuccess();

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("PlanStepRunner",
            Make.Step("foreach %plan.steps% item=%planStep%, set %seen% = %planStep%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%plan.steps%"), Make.Param("item", "%planStep%", "variable")),
                Make.Action("variable", "set", Make.Param("Name", "seen", "variable"), Make.Param("Value", "%planStep%", "variable")))));
        var result = await goal.Step[0].Start(context);

        await result.IsSuccess();
        var seen = await context.Variable.Get("seen");   // last step (index 1)
        var idx = await (await seen.Get("index")).Value();
        await Assert.That(idx?.ToString()).IsEqualTo("1");
    }
}
