using App;
using global::App.Errors;

namespace PLang.Tests.App.Core;

public class CallStackTests
{
    private static global::App.Goals.Goal.Steps.Step.Actions.Action.@this MakeAction(Goal goal)
    {
        var step = new Step { Index = 0, Text = "test", Goal = goal };
        var action = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this { Module = "test", ActionName = "test" };
        action.Step = step;
        return action;
    }

    private static global::App.Goals.Goal.Steps.Step.Actions.Action.@this MakeAction(string goalName)
        => MakeAction(new Goal { Name = goalName });

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

        var frame = stack.Push(MakeAction("TestGoal"));

        await Assert.That(stack.Depth).IsEqualTo(1);
        await Assert.That(stack.Current).IsEqualTo(frame);
    }

    [Test]
    public async Task Push_SetsGoal()
    {
        var stack = new CallStack();
        var goal = new Goal { Name = "TestGoal" };

        var frame = stack.Push(MakeAction(goal));

        await Assert.That(frame.Action.Step!.Goal!).IsEqualTo(goal);
        await Assert.That(frame.Action.Step!.Goal!.Name).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Push_SetsGoalWithPath()
    {
        var stack = new CallStack();
        var goal = new Goal { Name = "TestGoal", Path = "/path/to/goal.pr" };

        var frame = stack.Push(MakeAction(goal));

        await Assert.That(frame.Action.Step!.Goal!.Path).IsEqualTo("/path/to/goal.pr");
    }

    [Test]
    public async Task Push_SetsParent()
    {
        var stack = new CallStack();
        var parent = stack.Push(MakeAction("ParentGoal"));

        var child = stack.Push(MakeAction("ChildGoal"));

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task Push_WhenDisabled_ReturnsNewFrame()
    {
        var stack = new CallStack { IsEnabled = false };

        var frame = stack.Push(MakeAction("TestGoal"));

        await Assert.That(frame).IsNotNull();
        await Assert.That(stack.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task Push_ExceedsMaxDepth_ThrowsException()
    {
        var stack = new CallStack { MaxDepth = 3 };
        stack.Push(MakeAction("Goal1"));
        stack.Push(MakeAction("Goal2"));
        stack.Push(MakeAction("Goal3"));

        await Assert.ThrowsAsync<CallStackOverflowException>(async () =>
        {
            await Task.Run(() => stack.Push(MakeAction("Goal4")));
        });
    }

    [Test]
    public async Task Pop_RemovesFrame()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("TestGoal"));

        var frame = await stack.PopAsync();

        await Assert.That(stack.Depth).IsEqualTo(0);
        await Assert.That(stack.Current).IsNull();
    }

    [Test]
    public async Task Pop_ReturnsFrame()
    {
        var stack = new CallStack();
        var pushed = stack.Push(MakeAction("TestGoal"));

        var popped = await stack.PopAsync();

        await Assert.That(popped).IsEqualTo(pushed);
    }

    [Test]
    public async Task Pop_CompletesFrame()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("TestGoal"));

        var frame = await stack.PopAsync();

        await Assert.That(frame!.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Pop_EmptyStack_ReturnsNull()
    {
        var stack = new CallStack();

        var frame = await stack.PopAsync();

        await Assert.That(frame).IsNull();
    }

    [Test]
    public async Task Peek_ReturnsCurrent()
    {
        var stack = new CallStack();
        var pushed = stack.Push(MakeAction("TestGoal"));

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
    public async Task Errors_Add_TracksErrors()
    {
        var stack = new CallStack();
        var error = new Error("Test error");

        stack.Errors.Add(error);

        await Assert.That(stack.Errors.Count).IsEqualTo(1);
        await Assert.That(stack.Errors[0]).IsEqualTo(error);
    }

    [Test]
    public async Task Errors_MultipleAdds()
    {
        var stack = new CallStack();
        stack.Errors.Add(new Error("Error 1"));
        stack.Errors.Add(new Error("Error 2"));

        await Assert.That(stack.Errors.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Errors_Clear_RemovesAll()
    {
        var stack = new CallStack();
        stack.Errors.Add(new Error("Error 1"));
        stack.Errors.Add(new Error("Error 2"));

        stack.Errors.Clear();

        await Assert.That(stack.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PushError_CreatesFrameWhenDisabled()
    {
        var stack = new CallStack { IsEnabled = false };
        var error = new Error("Test error");
        var action = MakeAction("FailingGoal");

        var frame = stack.PushError(action, error);

        await Assert.That(frame).IsNotNull();
        await Assert.That(frame.Error).IsEqualTo(error);
        await Assert.That(frame.Errors.Count).IsEqualTo(1);
        await Assert.That(stack.Errors.Count).IsEqualTo(1);
        await Assert.That(stack.Current).IsEqualTo(frame);
    }

    [Test]
    public async Task PushError_CreatesFrameWhenEnabled()
    {
        var stack = new CallStack();
        var error = new Error("Test error");
        var action = MakeAction("FailingGoal");

        var frame = stack.PushError(action, error);

        await Assert.That(frame.Error).IsEqualTo(error);
        await Assert.That(stack.Errors.Count).IsEqualTo(1);
        await Assert.That(stack.Current).IsEqualTo(frame);
    }

    [Test]
    public async Task GetFrames_ReturnsAllFrames()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("Goal1"));
        stack.Push(MakeAction("Goal2"));
        stack.Push(MakeAction("Goal3"));

        var frames = stack.GetFrames();

        await Assert.That(frames.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetStackTrace_ReturnsFormattedTrace()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("Goal1"));
        stack.Push(MakeAction("Goal2"));

        var trace = stack.GetStackTrace();

        await Assert.That(trace).Contains("Goal1");
        await Assert.That(trace).Contains("Goal2");
    }

    [Test]
    public async Task GetStackTrace_EmptyStack_ReturnsNoTraceMessage()
    {
        var stack = new CallStack();

        var trace = stack.GetStackTrace();

        await Assert.That(trace).IsEqualTo("(no stack trace available)");
    }

    [Test]
    public async Task IsInEvent_ReturnsFalse_WhenNotInEvent()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("TestGoal"));

        await Assert.That(stack.IsInEvent).IsFalse();
    }

    [Test]
    public async Task IsInEvent_ReturnsTrue_WhenCurrentFrameHasEventId()
    {
        var stack = new CallStack();
        var frame = stack.Push(MakeAction("TestGoal"));
        frame.EventId = "event123";

        await Assert.That(stack.IsInEvent).IsTrue();
    }

    [Test]
    public async Task ContainsGoal_ReturnsTrue_WhenGoalInStack()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("Goal1"));
        stack.Push(MakeAction("Goal2"));

        await Assert.That(stack.ContainsGoal("Goal1")).IsTrue();
    }

    [Test]
    public async Task ContainsGoal_ReturnsFalse_WhenGoalNotInStack()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("Goal1"));

        await Assert.That(stack.ContainsGoal("Goal2")).IsFalse();
    }

    [Test]
    public async Task ContainsGoal_CaseInsensitive()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("TestGoal"));

        await Assert.That(stack.ContainsGoal("testgoal")).IsTrue();
        await Assert.That(stack.ContainsGoal("TESTGOAL")).IsTrue();
    }

    [Test]
    public async Task Clear_RemovesAllFramesAndErrors()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("Goal1"));
        stack.Push(MakeAction("Goal2"));
        stack.Errors.Add(new Error("Error"));

        stack.Clear();

        await Assert.That(stack.Depth).IsEqualTo(0);
        await Assert.That(stack.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ToSerializable_ReturnsSerializableRepresentation()
    {
        var stack = new CallStack();
        stack.Push(MakeAction("Goal1"));
        stack.Push(MakeAction("Goal2"));

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
