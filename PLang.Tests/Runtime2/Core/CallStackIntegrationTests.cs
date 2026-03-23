using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Core;

public class CallStackIntegrationTests
{
    private PLang.Runtime2.Engine.@this _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new PLang.Runtime2.Engine.@this("/test");
    }

    [Test]
    public async Task SingleGoal_DepthIsOne_DuringStepExecution()
    {
        int depthDuringExecution = -1;

        var step = new Step { Index = 0, Text = "probe step" };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        _engine.Context.User.Events.Register(
            EventType.BeforeStep,
            ctx =>
            {
                depthDuringExecution = ctx.CallStack!.Depth;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal",
            stepPattern: "probe step");

        await _engine.RunGoalAsync(goal);

        // During step execution depth was 1 (goal frame pushed)
        await Assert.That(depthDuringExecution).IsEqualTo(1);

        // After execution depth is 0 (frame popped)
        await Assert.That(_engine.Context.CallStack!.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task StepRecording_AfterSteps_FrameHasExecutedSteps()
    {
        var executedSteps = new List<string>();

        var step1 = new Step { Index = 0, Text = "first step" };
        var step2 = new Step { Index = 1, Text = "second step" };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step1, step2 }
        };

        _engine.Context.User.Events.Register(
            EventType.AfterStep,
            ctx =>
            {
                executedSteps.Add(ctx.CallStack!.Current!.Step!.Text);
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        await _engine.RunGoalAsync(goal);

        await Assert.That(executedSteps.Count).IsEqualTo(2);
        await Assert.That(executedSteps[0]).IsEqualTo("first step");
        await Assert.That(executedSteps[1]).IsEqualTo("second step");
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
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
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
    public async Task CallStackDepth_AccessibleAsContextVariable()
    {
        int depthFromContextVar = -1;

        var step = new Step { Index = 0, Text = "probe step" };

        var goal = new Goal
        {
            Name = "DepthGoal",
            Steps = new GoalSteps { step }
        };

        _engine.Context.User.Events.Register(
            EventType.BeforeStep,
            ctx =>
            {
                var depthData = ctx.MemoryStack.Get("!callStack.Depth");
                if (depthData?.Value is int d)
                    depthFromContextVar = d;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "DepthGoal",
            stepPattern: "probe step");

        await _engine.RunGoalAsync(goal);

        await Assert.That(depthFromContextVar).IsEqualTo(1);
    }

    [Test]
    public async Task CallStack_Push_IncreasesDepth()
    {
        var callStack = new CallStack();

        await Assert.That(callStack.Depth).IsEqualTo(0);

        callStack.Push("Goal1");
        await Assert.That(callStack.Depth).IsEqualTo(1);

        callStack.Push("Goal2");
        await Assert.That(callStack.Depth).IsEqualTo(2);
    }

    [Test]
    public async Task CallStack_Pop_DecreasesDepth()
    {
        var callStack = new CallStack();
        callStack.Push("Goal1");
        callStack.Push("Goal2");

        await callStack.PopAsync();
        await Assert.That(callStack.Depth).IsEqualTo(1);

        await callStack.PopAsync();
        await Assert.That(callStack.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task CallStack_RecordStep_TracksInCurrentFrame()
    {
        var callStack = new CallStack();
        callStack.Push("TestGoal");

        callStack.RecordStep(new Step { Index = 0, Text = "step one" });
        callStack.RecordStep(new Step { Index = 1, Text = "step two" });

        var frame = callStack.Current!;
        await Assert.That(frame.ExecutedSteps.Count).IsEqualTo(2);
        await Assert.That(frame.ExecutedSteps[0].Text).IsEqualTo("step one");
        await Assert.That(frame.ExecutedSteps[1].Text).IsEqualTo("step two");
    }

    [Test]
    public async Task CallStack_ContainsGoal_FindsActiveGoal()
    {
        var callStack = new CallStack();
        callStack.Push("Goal1");
        callStack.Push("Goal2");

        await Assert.That(callStack.ContainsGoal("Goal1")).IsTrue();
        await Assert.That(callStack.ContainsGoal("Goal2")).IsTrue();
        await Assert.That(callStack.ContainsGoal("Goal3")).IsFalse();
    }
}
