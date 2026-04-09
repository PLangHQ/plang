using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.modules.loop;
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
        var context = _app.Context;
        var items = new List<object?> { "a", "b", "c" };
        context.Variables.Set("items", items);

        // Register a goal that the goal.call action will invoke
        var goal = new Goal
        {
            Name = "ProcessItem",
            Path = "/ProcessItem.goal",
            Steps = new GoalSteps()
        };
        _app.Goals.Add(goal);

        // Build a step with foreach + goal.call (the orchestration pattern)
        var foreachAction = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "loop",
            ActionName = "foreach",
            Parameters = new List<Data>
            {
                new Data("collection", "%items%"),
                new Data("itemname", "%item%")
            }
        };
        var goalCallAction = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "goal",
            ActionName = "call",
            Parameters = new List<Data>
            {
                new Data("goalname", new Dictionary<string, object?> { ["name"] = "ProcessItem" })
            }
        };

        var step = new Step
        {
            Index = 0,
            Text = "foreach %items%, call ProcessItem item=%item%",
            Actions = new StepActions { foreachAction, goalCallAction }
        };
        foreachAction.Step = step;
        goalCallAction.Step = step;

        // Run via the step (which triggers ExecuteAsync → Run → orchestration)
        var result = await step.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        // After the loop, item should be the last value
        await Assert.That(context.Variables.GetValue("item")).IsEqualTo("c");
    }

    [Test]
    public async Task Foreach_EmptyCollection_ReturnsZeroCount()
    {
        var context = _app.Context;
        var items = new List<object?>();

        var action = new Foreach
        {
            Context = context,
            Collection = items,
            ItemName = "item"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(0);
        await Assert.That(loopResult.completed).IsTrue();
    }

    [Test]
    public async Task Foreach_SetsItemVariable()
    {
        var context = _app.Context;
        var items = new List<object?> { "hello" };

        // Build step with foreach + output.write to verify item is set
        var foreachAction = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "loop",
            ActionName = "foreach",
            Parameters = new List<Data>
            {
                new Data("collection", items),
                new Data("itemname", "%myItem%")
            }
        };
        var writeAction = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "output",
            ActionName = "write",
            Parameters = new List<Data>
            {
                new Data("data", "%myItem%")
            }
        };

        var step = new Step
        {
            Index = 0,
            Text = "foreach %items%, write out %myItem%",
            Actions = new StepActions { foreachAction, writeAction }
        };
        foreachAction.Step = step;
        writeAction.Step = step;

        var result = await step.RunAsync(context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("myItem")).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_IteratesDictionary()
    {
        var context = _app.Context;
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };

        var action = new Foreach
        {
            Context = context,
            Collection = dict,
            ItemName = "val",
            KeyName = "key"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(2);
    }

    [Test]
    public async Task Foreach_VerifiesEachItemVisited()
    {
        var context = _app.Context;
        var items = new List<object?> { "alpha", "beta", "gamma" };

        // Foreach with no body actions — just iterates and sets item variable
        var action = new Foreach
        {
            Context = context,
            Collection = items,
            ItemName = "item"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("item")).IsEqualTo("gamma");

        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(3);
        await Assert.That(loopResult.completed).IsTrue();
    }

    [Test]
    public async Task Foreach_NullCollection_ReturnsZeroCount()
    {
        var context = _app.Context;

        var action = new Foreach
        {
            Context = context,
            Collection = null,
            ItemName = "item"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(0);
        await Assert.That(loopResult.completed).IsTrue();
    }

    [Test]
    public async Task Foreach_Cancellation_StopsIteration()
    {
        var context = _app.Context;
        var items = new List<object?> { "a", "b", "c", "d", "e" };

        var cts = new CancellationTokenSource();
        context.PushCancellation(cts);
        cts.Cancel(); // Cancel immediately

        var action = new Foreach
        {
            Context = context,
            Collection = items,
            ItemName = "item"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.completed).IsFalse();
        await Assert.That(loopResult.itemCount).IsEqualTo(0);
    }
}
