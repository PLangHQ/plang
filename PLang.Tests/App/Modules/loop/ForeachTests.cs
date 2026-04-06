using App.Engine.Context;
using App.Engine;
using App.Engine.Variables;
using App.modules.loop;
using LoopResult = App.modules.loop.types.loop;

namespace PLang.Tests.App.actions.loop;

public class ForeachTests
{
    private App.Engine.@this _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new App.Engine.@this("/app");
    }

    private PLangContext CreateContext(Variables? memory = null)
    {
        var context = _engine.CreateContext(memory);
        return context;
    }

    [Test]
    public async Task Foreach_IteratesList()
    {
        var context = CreateContext();
        var items = new List<object?> { "a", "b", "c" };
        context.Variables.Set("items", items);

        // Register a goal that captures the item value
        var captured = new List<object?>();
        var captureGoal = new Goal
        {
            Name = "ProcessItem",
            Path = "/ProcessItem.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "capture item",
                    Actions = new StepActions
                    {
                        new App.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", "%captured%"),
                                new Data("value", "%item%")
                            }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(captureGoal);

        var action = new Foreach
        {
            Context = context,
            Collection = items,
            GoalName = new GoalCall { Name = "ProcessItem" },
            ItemName = "item"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult).IsNotNull();
        await Assert.That(loopResult!.itemCount).IsEqualTo(3);
        await Assert.That(loopResult.completed).IsTrue();
    }

    [Test]
    public async Task Foreach_EmptyCollection_ReturnsZeroCount()
    {
        var context = CreateContext();
        var items = new List<object?>();

        var action = new Foreach
        {
            Context = context,
            Collection = items,
            GoalName = new GoalCall { Name = "DoNothing" },
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
        var context = CreateContext();
        var items = new List<object?> { "hello" };

        // Simple goal that does nothing (just captures that item was set)
        var goal = new Goal
        {
            Name = "CaptureGoal",
            Path = "/CaptureGoal.goal",
            Steps = new GoalSteps()
        };
        _engine.Goals.Add(goal);

        var action = new Foreach
        {
            Context = context,
            Collection = items,
            GoalName = new GoalCall { Name = "CaptureGoal" },
            ItemName = "myItem"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // After the loop, the item variable should still be set to the last value
        await Assert.That(context.Variables.GetValue("myItem")).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_IteratesDictionary()
    {
        var context = CreateContext();
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };

        var goal = new Goal { Name = "DictGoal", Path = "/DictGoal.goal", Steps = new GoalSteps() };
        _engine.Goals.Add(goal);

        var action = new Foreach
        {
            Context = context,
            Collection = dict,
            GoalName = new GoalCall { Name = "DictGoal" },
            ItemName = "val",
            KeyName = "key"
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loopResult = result.Value as LoopResult;
        await Assert.That(loopResult!.itemCount).IsEqualTo(2);
    }
}
