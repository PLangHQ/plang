using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.actions.loop;
using LoopResult = PLang.Runtime2.actions.loop.types.loop;

namespace PLang.Tests.Runtime2.actions.loop;

public class ForeachTests
{
    private Engine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new Engine("/app");
    }

    private PLangContext CreateContext(MemoryStack? memory = null)
    {
        var context = _engine.CreateContext(memory);
        return context;
    }

    [Test]
    public async Task Foreach_IteratesList()
    {
        var context = CreateContext();
        var items = new List<object?> { "a", "b", "c" };
        context.MemoryStack.Set("items", items);

        // Register a goal that captures the item value
        var captured = new List<object?>();
        var captureGoal = new Goal
        {
            Name = "ProcessItem",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "capture item",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Action
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
        await Assert.That(context.MemoryStack.GetValue("myItem")).IsEqualTo("hello");
    }

    [Test]
    public async Task Foreach_IteratesDictionary()
    {
        var context = CreateContext();
        var dict = new Dictionary<string, object?> { ["name"] = "Alice", ["age"] = 30 };

        var goal = new Goal { Name = "DictGoal", Steps = new GoalSteps() };
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
