using global::App.Actor.Context;
using App;
using global::App.Variables;
using LoopResult = global::App.modules.loop.types.loop;

namespace PLang.Tests.App.actions.loop;

public class ForeachTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    [Test]
    public async Task Foreach_OrchestatesGoalCall()
    {
        var context = _app.User.Context;
        var items = new List<object?> { "a", "b", "c" };
        context.Variables.Set("items", items);

        var goal = new Goal
        {
            Name = "ProcessItem",
            Path = "/ProcessItem.goal",
            Steps = new GoalSteps()
        };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("itemname", "%item%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "ProcessItem" }));

        var step = new Step
        {
            Index = 0,
            Text = "foreach %items%, call ProcessItem item=%item%",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("item")).IsEqualTo("c");
    }

    [Test]
    public async Task Foreach_EmptyCollection_ReturnsZeroCount()
    {
        var context = _app.User.Context;
        context.Variables.Set("items", new List<object?>());

        var action = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("itemname", "%item%"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(0);
        await Assert.That(loopResult.completed).IsTrue();
    }

    [Test]
    public async Task Foreach_SetsItemVariable()
    {
        var context = _app.User.Context;
        context.Variables.Set("items", new List<object?> { "hello" });

        var goal = new Goal { Name = "DoNothing", Path = "/DoNothing.goal", Steps = new GoalSteps() };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("itemname", "%myItem%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "DoNothing" }));

        var step = new Step
        {
            Index = 0,
            Text = "foreach %items%, call DoNothing item=%myItem%",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("myItem")).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_IteratesDictionary()
    {
        var context = _app.User.Context;
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };
        context.Variables.Set("dict", dict);

        var goal = new Goal { Name = "DictGoal", Path = "/DictGoal.goal", Steps = new GoalSteps() };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%dict%"), ("itemname", "%val%"), ("keyname", "%key%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "DictGoal" }));

        var step = new Step
        {
            Index = 0,
            Text = "foreach %dict%, call DictGoal item=%val%",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Foreach_Dictionary_KeyIsStringNotIndex()
    {
        var context = _app.User.Context;
        // Use single-entry dict so final state = only iteration
        var dict = new Dictionary<string, object?> { ["greeting"] = "hello" };
        context.Variables.Set("dict", dict);

        var goal = new Goal { Name = "Noop", Path = "/Noop.goal", Steps = new GoalSteps() };
        _app.Goals.Add(goal);

        var foreachAction = TestAction.Create("loop", "foreach",
            ("collection", "%dict%"), ("itemname", "%val%"), ("keyname", "%key%"));
        var goalCallAction = TestAction.Create("goal", "call",
            ("goalname", new Dictionary<string, object?> { ["name"] = "Noop" }));

        var step = new Step
        {
            Index = 0,
            Text = "foreach %dict%, call Noop",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        var result = await step.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        // %key% should be the dictionary key (string "greeting"), not numeric index (0)
        var key = context.Variables.GetValue("key");
        await Assert.That(key).IsEqualTo("greeting");
        // %val% should be the value ("hello"), not a KeyValuePair struct
        var val = context.Variables.GetValue("val");
        await Assert.That(val).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_NullCollection_ReturnsZeroCount()
    {
        var context = _app.User.Context;

        var action = TestAction.Create("loop", "foreach",
            ("collection", null), ("itemname", "%item%"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(0);
        await Assert.That(loopResult.completed).IsTrue();
    }

    [Test]
    public async Task Foreach_Cancellation_StopsIteration()
    {
        var context = _app.User.Context;
        context.Variables.Set("items", new List<object?> { "a", "b", "c", "d", "e" });

        var cts = new CancellationTokenSource();
        context.PushCancellation(cts);
        cts.Cancel();

        var action = TestAction.Create("loop", "foreach",
            ("collection", "%items%"), ("itemname", "%item%"));
        var result = await _app.Run(action, context);

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.completed).IsFalse();
        await Assert.That(loopResult.itemCount).IsEqualTo(0);
    }
}
