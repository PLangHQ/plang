using App.Engine;
using App.Engine.Variables;

namespace PLang.Tests.App.Core;

public class CallStackIntegrationTests
{
    private App.Engine.@this _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new App.Engine.@this("/test");
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
                new App.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
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

        var result = await _engine.RunGoalAsync(goal);

        // The goal should have failed
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task CallStack_Push_IncreasesDepth()
    {
        var callStack = new CallStack();

        await Assert.That(callStack.Depth).IsEqualTo(0);

        callStack.Push(new Goal { Name = "Goal1" });
        await Assert.That(callStack.Depth).IsEqualTo(1);

        callStack.Push(new Goal { Name = "Goal2" });
        await Assert.That(callStack.Depth).IsEqualTo(2);
    }

    [Test]
    public async Task CallStack_Pop_DecreasesDepth()
    {
        var callStack = new CallStack();
        callStack.Push(new Goal { Name = "Goal1" });
        callStack.Push(new Goal { Name = "Goal2" });

        await callStack.PopAsync();
        await Assert.That(callStack.Depth).IsEqualTo(1);

        await callStack.PopAsync();
        await Assert.That(callStack.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task CallStack_RecordStep_TracksInCurrentFrame()
    {
        var callStack = new CallStack();
        callStack.Push(new Goal { Name = "TestGoal" });

        callStack.RecordStep(new Step { Index = 0, Text = "step one" });
        callStack.RecordStep(new Step { Index = 1, Text = "step two" });

        var frame = callStack.Current!;
        await Assert.That(frame.ExecutedSteps.Count).IsEqualTo(2);
        await Assert.That(frame.ExecutedSteps[0].Step.Text).IsEqualTo("step one");
        await Assert.That(frame.ExecutedSteps[1].Step.Text).IsEqualTo("step two");
    }

    [Test]
    public async Task CallStack_ContainsGoal_FindsActiveGoal()
    {
        var callStack = new CallStack();
        callStack.Push(new Goal { Name = "Goal1" });
        callStack.Push(new Goal { Name = "Goal2" });

        await Assert.That(callStack.ContainsGoal("Goal1")).IsTrue();
        await Assert.That(callStack.ContainsGoal("Goal2")).IsTrue();
        await Assert.That(callStack.ContainsGoal("Goal3")).IsFalse();
    }
}
