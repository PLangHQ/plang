using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;

namespace PLang.Tests.Runtime2.Core;

public class CallStackTests
{
    [Test]
    public async Task Constructor_DefaultsToEnabled()
    {
        var stack = new CallStack();

        await Assert.That(stack.IsEnabled).IsTrue();
    }

    [Test]
    public async Task Constructor_DefaultsMaxDepthTo1000()
    {
        var stack = new CallStack();

        await Assert.That(stack.MaxDepth).IsEqualTo(1000);
    }

    [Test]
    public async Task Constructor_StartsEmpty()
    {
        var stack = new CallStack();

        await Assert.That(stack.Depth).IsEqualTo(0);
        await Assert.That(stack.Current).IsNull();
    }

    [Test]
    public async Task Push_AddsFrame()
    {
        var stack = new CallStack();

        var frame = stack.Push("TestGoal");

        await Assert.That(stack.Depth).IsEqualTo(1);
        await Assert.That(stack.Current).IsEqualTo(frame);
    }

    [Test]
    public async Task Push_SetsGoalName()
    {
        var stack = new CallStack();

        var frame = stack.Push("TestGoal");

        await Assert.That(frame.GoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Push_SetsGoalPath()
    {
        var stack = new CallStack();

        var frame = stack.Push("TestGoal", "/path/to/goal.pr");

        await Assert.That(frame.GoalPath).IsEqualTo("/path/to/goal.pr");
    }

    [Test]
    public async Task Push_SetsParent()
    {
        var stack = new CallStack();
        var parent = stack.Push("ParentGoal");

        var child = stack.Push("ChildGoal");

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task Push_WhenDisabled_ReturnsNewFrame()
    {
        var stack = new CallStack { IsEnabled = false };

        var frame = stack.Push("TestGoal");

        await Assert.That(frame).IsNotNull();
        await Assert.That(stack.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task Push_ExceedsMaxDepth_ThrowsException()
    {
        var stack = new CallStack { MaxDepth = 3 };
        stack.Push("Goal1");
        stack.Push("Goal2");
        stack.Push("Goal3");

        await Assert.ThrowsAsync<CallStackOverflowException>(async () =>
        {
            await Task.Run(() => stack.Push("Goal4"));
        });
    }

    [Test]
    public async Task Pop_RemovesFrame()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");

        var frame = stack.Pop();

        await Assert.That(stack.Depth).IsEqualTo(0);
        await Assert.That(stack.Current).IsNull();
    }

    [Test]
    public async Task Pop_ReturnsFrame()
    {
        var stack = new CallStack();
        var pushed = stack.Push("TestGoal");

        var popped = stack.Pop();

        await Assert.That(popped).IsEqualTo(pushed);
    }

    [Test]
    public async Task Pop_CompletesFrame()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");

        var frame = stack.Pop();

        await Assert.That(frame!.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Pop_WhenDisabled_ReturnsNull()
    {
        var stack = new CallStack { IsEnabled = false };
        stack.Push("TestGoal");

        var frame = stack.Pop();

        await Assert.That(frame).IsNull();
    }

    [Test]
    public async Task Pop_EmptyStack_ReturnsNull()
    {
        var stack = new CallStack();

        var frame = stack.Pop();

        await Assert.That(frame).IsNull();
    }

    [Test]
    public async Task Peek_ReturnsCurrent()
    {
        var stack = new CallStack();
        var pushed = stack.Push("TestGoal");

        var peeked = stack.Peek();

        await Assert.That(peeked).IsEqualTo(pushed);
        await Assert.That(stack.Depth).IsEqualTo(1);
    }

    [Test]
    public async Task Peek_EmptyStack_ReturnsNull()
    {
        var stack = new CallStack();

        var peeked = stack.Peek();

        await Assert.That(peeked).IsNull();
    }

    [Test]
    public async Task RecordStep_UpdatesCurrentFrame()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");
        var step = new Step { Index = 5, Text = "test step", LineNumber = 6 };

        stack.RecordStep(step);

        await Assert.That(stack.Current!.Step).IsEqualTo(step);
        await Assert.That(stack.Current!.Step!.Index).IsEqualTo(5);
        await Assert.That(stack.Current!.Step!.Text).IsEqualTo("test step");
    }

    [Test]
    public async Task RecordStep_AddsExecutedStep()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");
        var step = new Step { Index = 0, Text = "test step", LineNumber = 1 };

        stack.RecordStep(step);

        await Assert.That(stack.Current!.ExecutedSteps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RecordStep_WhenDisabled_DoesNothing()
    {
        var stack = new CallStack { IsEnabled = false };
        stack.Push("TestGoal");
        var step = new Step { Index = 0, Text = "test step", LineNumber = 1 };

        stack.RecordStep(step);

        // No exception and no updates (stack is disabled so no current frame)
    }

    [Test]
    public async Task RecordStep_NoCurrentFrame_DoesNotThrow()
    {
        var stack = new CallStack();
        var step = new Step { Index = 0, Text = "test step", LineNumber = 1 };

        stack.RecordStep(step);

        await Assert.That(stack.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task AddError_AddsToErrorList()
    {
        var stack = new CallStack();
        var error = new Error("Test error");

        stack.AddError(error);

        var errors = stack.GetErrors();
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0]).IsEqualTo(error);
    }

    [Test]
    public async Task AddError_AddsToCurrentFrame()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");
        var error = new Error("Test error");

        stack.AddError(error);

        await Assert.That(stack.Current!.Errors.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddError_WhenDisabled_AddsToGlobalListOnly()
    {
        var stack = new CallStack { IsEnabled = false };
        var error = new Error("Test error");

        stack.AddError(error);

        var errors = stack.GetErrors();
        await Assert.That(errors.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetErrors_ReturnsAllErrors()
    {
        var stack = new CallStack();
        stack.AddError(new Error("Error 1"));
        stack.AddError(new Error("Error 2"));

        var errors = stack.GetErrors();

        await Assert.That(errors.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ClearErrors_RemovesAllErrors()
    {
        var stack = new CallStack();
        stack.AddError(new Error("Error 1"));
        stack.AddError(new Error("Error 2"));

        stack.ClearErrors();

        var errors = stack.GetErrors();
        await Assert.That(errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetFrames_ReturnsAllFrames()
    {
        var stack = new CallStack();
        stack.Push("Goal1");
        stack.Push("Goal2");
        stack.Push("Goal3");

        var frames = stack.GetFrames();

        await Assert.That(frames.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetStackTrace_ReturnsFormattedTrace()
    {
        var stack = new CallStack();
        stack.Push("Goal1");
        stack.Push("Goal2");

        var trace = stack.GetStackTrace();

        await Assert.That(trace).Contains("Goal1");
        await Assert.That(trace).Contains("Goal2");
    }

    [Test]
    public async Task GetStackTrace_WhenDisabled_ReturnsNoTraceMessage()
    {
        var stack = new CallStack { IsEnabled = false };

        var trace = stack.GetStackTrace();

        await Assert.That(trace).IsEqualTo("(no stack trace available)");
    }

    [Test]
    public async Task GetStackTrace_EmptyStack_ReturnsNoTraceMessage()
    {
        var stack = new CallStack();

        var trace = stack.GetStackTrace();

        await Assert.That(trace).IsEqualTo("(no stack trace available)");
    }

    [Test]
    public async Task GetExecutionHistory_ReturnsFrameStepPairs()
    {
        var stack = new CallStack();
        stack.Push("Goal1");
        stack.RecordStep(new Step { Index = 0, Text = "step1" });
        stack.Push("Goal2");
        stack.RecordStep(new Step { Index = 0, Text = "step2" });

        var history = stack.GetExecutionHistory().ToList();

        await Assert.That(history.Count).IsEqualTo(2);
    }

    [Test]
    public async Task IsInEvent_ReturnsFalse_WhenNotInEvent()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");

        await Assert.That(stack.IsInEvent).IsFalse();
    }

    [Test]
    public async Task IsInEvent_ReturnsTrue_WhenCurrentFrameHasEventId()
    {
        var stack = new CallStack();
        var frame = stack.Push("TestGoal");
        frame.EventId = "event123";

        await Assert.That(stack.IsInEvent).IsTrue();
    }

    [Test]
    public async Task ContainsGoal_ReturnsTrue_WhenGoalInStack()
    {
        var stack = new CallStack();
        stack.Push("Goal1");
        stack.Push("Goal2");

        await Assert.That(stack.ContainsGoal("Goal1")).IsTrue();
    }

    [Test]
    public async Task ContainsGoal_ReturnsFalse_WhenGoalNotInStack()
    {
        var stack = new CallStack();
        stack.Push("Goal1");

        await Assert.That(stack.ContainsGoal("Goal2")).IsFalse();
    }

    [Test]
    public async Task ContainsGoal_CaseInsensitive()
    {
        var stack = new CallStack();
        stack.Push("TestGoal");

        await Assert.That(stack.ContainsGoal("testgoal")).IsTrue();
        await Assert.That(stack.ContainsGoal("TESTGOAL")).IsTrue();
    }

    [Test]
    public async Task Clear_RemovesAllFramesAndErrors()
    {
        var stack = new CallStack();
        stack.Push("Goal1");
        stack.Push("Goal2");
        stack.AddError(new Error("Error"));

        stack.Clear();

        await Assert.That(stack.Depth).IsEqualTo(0);
        await Assert.That(stack.GetErrors().Count).IsEqualTo(0);
    }

    [Test]
    public async Task ToSerializable_ReturnsSerializableRepresentation()
    {
        var stack = new CallStack();
        stack.Push("Goal1");
        stack.Push("Goal2");

        var serializable = stack.ToSerializable();

        await Assert.That(serializable.Frames.Count).IsEqualTo(2);
        await Assert.That(serializable.Depth).IsEqualTo(2);
        await Assert.That(serializable.StackTrace).IsNotNull();
    }
}

public class SerializableCallStackTests
{
    [Test]
    public async Task Properties_CanBeSet()
    {
        var serializable = new SerializableCallStack
        {
            Frames = new List<SerializableCallFrame>(),
            Depth = 5,
            StackTrace = "test trace"
        };

        await Assert.That(serializable.Frames).IsNotNull();
        await Assert.That(serializable.Depth).IsEqualTo(5);
        await Assert.That(serializable.StackTrace).IsEqualTo("test trace");
    }
}

public class SerializableCallFrameTests
{
    [Test]
    public async Task Properties_CanBeSet()
    {
        var frame = new SerializableCallFrame
        {
            Id = "abc123",
            GoalName = "TestGoal",
            GoalPath = "/path/to/goal",
            Phase = "ExecutingStep",
            CurrentStepIndex = 5,
            CurrentStepText = "test step",
            StartedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(1),
            Depth = 2,
            HasErrors = true
        };

        await Assert.That(frame.Id).IsEqualTo("abc123");
        await Assert.That(frame.GoalName).IsEqualTo("TestGoal");
        await Assert.That(frame.GoalPath).IsEqualTo("/path/to/goal");
        await Assert.That(frame.Phase).IsEqualTo("ExecutingStep");
        await Assert.That(frame.CurrentStepIndex).IsEqualTo(5);
        await Assert.That(frame.CurrentStepText).IsEqualTo("test step");
        await Assert.That(frame.Duration.TotalSeconds).IsEqualTo(1);
        await Assert.That(frame.Depth).IsEqualTo(2);
        await Assert.That(frame.HasErrors).IsTrue();
    }
}
