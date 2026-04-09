using App;
using global::App.Variables;

namespace PLang.Tests.App.Core;

public class CallStackIntegrationTests
{
    private static global::App.Goals.Goal.Steps.Step.Actions.Action.@this MakeAction(string goalName)
    {
        var goal = new Goal { Name = goalName };
        var step = new Step { Index = 0, Text = "test", Goal = goal };
        var action = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this { Module = "test", ActionName = "test" };
        action.Step = step;
        return action;
    }

    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    [Test]
    public async Task ErrorTracking_FailedStep_RecordsErrorInCallStack()
    {
        var step = new Step
        {
            Index = 0,
            Text = "failing step",
            Actions = new StepActions(new[]
            {
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = "nonexistent",
                    ActionName = "fail"
                }
            })
        };

        var goal = new Goal
        {
            Name = "ErrorGoal",
            Steps = new GoalSteps { step }
        };

        var result = await _app.RunGoalAsync(goal);

        // The goal should have failed
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task CallStack_Push_IncreasesDepth()
    {
        var callStack = new CallStack();

        await Assert.That(callStack.Depth).IsEqualTo(0);

        callStack.Push(MakeAction("Goal1"));
        await Assert.That(callStack.Depth).IsEqualTo(1);

        callStack.Push(MakeAction("Goal2"));
        await Assert.That(callStack.Depth).IsEqualTo(2);
    }

    [Test]
    public async Task CallStack_Pop_DecreasesDepth()
    {
        var callStack = new CallStack();
        callStack.Push(MakeAction("Goal1"));
        callStack.Push(MakeAction("Goal2"));

        await callStack.PopAsync();
        await Assert.That(callStack.Depth).IsEqualTo(1);

        await callStack.PopAsync();
        await Assert.That(callStack.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task CallStack_ContainsGoal_FindsActiveGoal()
    {
        var callStack = new CallStack();
        callStack.Push(MakeAction("Goal1"));
        callStack.Push(MakeAction("Goal2"));

        await Assert.That(callStack.ContainsGoal("Goal1")).IsTrue();
        await Assert.That(callStack.ContainsGoal("Goal2")).IsTrue();
        await Assert.That(callStack.ContainsGoal("Goal3")).IsFalse();
    }
}
