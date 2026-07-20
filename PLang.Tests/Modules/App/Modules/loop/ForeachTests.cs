using app.actor.context;
using app;
using app.variable;

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

        _app.Goal.Add(new Goal { Name = "ProcessItem", Path = global::app.type.item.path.@this.Resolve("/ProcessItem.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("ForeachRunner",
            Make.Step("foreach %items%, call ProcessItem item=%item%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%items%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "ProcessItem" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("item"))).IsEqualTo("c");
    }

    [Test]
    public async Task Foreach_EmptyCollection_ReturnsZeroCount()
    {
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?>());

        var action = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("itemname", "%item%"));
        var result = await action.Run(context);

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

        _app.Goal.Add(new Goal { Name = "DoNothing", Path = global::app.type.item.path.@this.Resolve("/DoNothing.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("SetsItemRunner",
            Make.Step("foreach %items%, call DoNothing item=%myItem%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%items%"), Make.Param("itemname", "%myItem%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("myItem"))).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_IteratesDictionary()
    {
        var context = _app.User.Context;
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        context.Variable.Set("dict", dict);

        _app.Goal.Add(new Goal { Name = "DictGoal", Path = global::app.type.item.path.@this.Resolve("/DictGoal.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("DictRunner",
            Make.Step("foreach %dict%, call DictGoal item=%val%",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%dict%"), Make.Param("itemname", "%val%", "variable"), Make.Param("keyname", "%key%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "DictGoal" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(context);

        await result.IsSuccess();
    }

    [Test]
    public async Task Foreach_Dictionary_KeyIsStringNotIndex()
    {
        var context = _app.User.Context;
        // Use single-entry dict so final state = only iteration
        var dict = new Dictionary<string, object?> { ["greeting"] = "hello" };
        context.Variable.Set("dict", dict);

        _app.Goal.Add(new Goal { Name = "Noop", Path = global::app.type.item.path.@this.Resolve("/Noop.goal", global::PLang.Tests.TestApp.SharedContext), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("DictKeyRunner",
            Make.Step("foreach %dict%, call Noop",
                Make.Action("loop", "foreach",
                    Make.Template("collection", "%dict%"), Make.Param("itemname", "%val%", "variable"), Make.Param("keyname", "%key%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "Noop" })))));
        var step = goal.Step.list.First();

        var result = await step.Run(context);

        await result.IsSuccess();
        // %key% should be the dictionary key (string "greeting"), not numeric index (0)
        var key = await context.Variable.GetValue("key");
        await Assert.That(key).IsEqualTo("greeting");
        // %val% should be the value ("hello"), not a KeyValuePair struct
        var val = await context.Variable.GetValue("val");
        await Assert.That(val).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_NullCollection_ReturnsZeroCount()
    {
        var context = _app.User.Context;

        var action = TestAction.Create("loop", "foreach",
            ("collection", null), ("itemname", "%item%"));
        var result = await action.Run(context);

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
            ("collection", "%items%"), ("itemname", "%item%"));
        var result = await action.Run(context);

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
        // Born the way llm.query's producer door does: context.Ok(json, "json") → clr(json).
        var plan = await context.Ok(planJson, context.App.Type.Kind["json"]);
        plan.Name = "plan";
        await context.Variable.Set(plan);

        // The builder writes child keys onto %plan% between llm.query and the foreach
        // (set %plan.system% = ..., etc.). Replicate one such write onto the clr(json).
        var setChild = TestAction.Create("variable", "set",
            ("name", "%plan.system%"), ("value", "sys-prompt"));
        await (await setChild.Run(context)).IsSuccess();

        var action = TestAction.Create("loop", "foreach",
            ("collection", "%plan.steps%"), ("itemname", "%planStep%"));
        var result = await action.Run(context);

        await result.IsSuccess();
        var planStep = await context.Variable.Get("planStep");   // last step (index 1)
        var idx = await (await planStep.Get("index")).Value();
        await Assert.That(idx?.ToString()).IsEqualTo("1");
    }
}
