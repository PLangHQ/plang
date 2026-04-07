using App;
using global::App.Errors;

namespace PLang.Tests.App.Core;

public class CallFrameTests
{
    /// <summary>
    /// Creates a minimal Action wired to a Goal, matching the new CallFrame(Action) API.
    /// </summary>
    private static global::App.Goals.Goal.Steps.Step.Actions.Action.@this MakeAction(Goal goal)
    {
        var step = new Step { Index = 0, Text = "test", Goal = goal };
        var action = new global::App.Goals.Goal.Steps.Step.Actions.Action.@this { Module = "test", ActionName = "test" };
        action.Step = step;
        return action;
    }

    private static global::App.Goals.Goal.Steps.Step.Actions.Action.@this MakeAction(string goalName, string? path = null)
        => MakeAction(new Goal { Name = goalName, Path = path ?? "" });

    [Test]
    public async Task Constructor_SetsGoal()
    {
        var goal = new Goal { Name = "TestGoal" };
        var frame = new CallFrame(MakeAction(goal));

        await Assert.That(frame.Action.Step!.Goal).IsEqualTo(goal);
        await Assert.That(frame.Action.Step!.Goal!.Name).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Constructor_SetsGoalWithPath()
    {
        var goal = new Goal { Name = "TestGoal", Path = "/path/to/goal.pr" };
        var frame = new CallFrame(MakeAction(goal));

        await Assert.That(frame.Action.Step!.Goal!.Path).IsEqualTo("/path/to/goal.pr");
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.Id).IsNotNull();
        await Assert.That(frame.Id.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Constructor_SetsStartedAt()
    {
        var before = DateTime.UtcNow;

        var frame = new CallFrame(MakeAction("TestGoal"));

        var after = DateTime.UtcNow;
        await Assert.That(frame.StartedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(frame.StartedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_DefaultsPhaseToNone()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.None);
    }

    [Test]
    public async Task Constructor_SetsActionStep()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.Action.Step).IsNotNull();
        await Assert.That(frame.Action.Step!.Goal!.Name).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Constructor_SetsParent()
    {
        var parent = new CallFrame(MakeAction("ParentGoal"));
        var child = new CallFrame(MakeAction("ChildGoal"), parent: parent);

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task Constructor_SetsIndent()
    {
        var parent = new CallFrame(MakeAction("ParentGoal"));
        var child = new CallFrame(MakeAction("ChildGoal"), parent: parent);

        await Assert.That(parent.Indent).IsEqualTo(0);
        await Assert.That(child.Indent).IsEqualTo(1);
    }

    [Test]
    public async Task CompletedAt_DefaultsToNull()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.CompletedAt).IsNull();
    }

    [Test]
    public async Task Errors_DefaultsToEmpty()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Phase_CanBeSet()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        frame.Phase = ExecutionPhase.ExecutingGoal;

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.ExecutingGoal);
    }

    [Test]
    public async Task Step_CanBeSet()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        var step = new Step { Index = 5, Text = "call http endpoint", LineNumber = 6 };

        frame.Action.Step = step;

        await Assert.That(frame.Action.Step).IsEqualTo(step);
        await Assert.That(frame.Action.Step!.Index).IsEqualTo(5);
        await Assert.That(frame.Action.Step!.Text).IsEqualTo("call http endpoint");
    }

    [Test]
    public async Task EventId_CanBeSet()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        frame.EventId = "event123";

        await Assert.That(frame.EventId).IsEqualTo("event123");
    }

    [Test]
    public async Task Duration_ReturnsPositiveTimeSpan()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        await Task.Delay(10);

        var duration = frame.Duration;

        await Assert.That(duration.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Complete_SetsCompletedAt()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        frame.Complete();

        await Assert.That(frame.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Complete_SetsPhaseToNone_WhenNoErrors()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        frame.Complete();

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.None);
    }

    [Test]
    public async Task Complete_SetsPhaseToError_WhenHasErrors()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        frame.Errors.Add(new Error("Test error"));

        frame.Complete();

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.Error);
    }

    [Test]
    public async Task Errors_Add_AddsToList()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        var error = new Error("Test error");

        frame.Errors.Add(error);

        await Assert.That(frame.Errors.Count).IsEqualTo(1);
        await Assert.That(frame.Errors[0]).IsEqualTo(error);
    }

    [Test]
    public async Task IsInEvent_ReturnsFalse_WhenNoEventId()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.IsInEvent).IsFalse();
    }

    [Test]
    public async Task IsInEvent_ReturnsTrue_WhenHasEventId()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        frame.EventId = "event123";

        await Assert.That(frame.IsInEvent).IsTrue();
    }

    [Test]
    public async Task IsInEvent_ReturnsTrue_WhenParentIsInEvent()
    {
        var parent = new CallFrame(MakeAction("ParentGoal"));
        parent.EventId = "event123";
        var child = new CallFrame(MakeAction("ChildGoal"), parent: parent);

        await Assert.That(child.IsInEvent).IsTrue();
    }

    [Test]
    public async Task Depth_ReturnsZero_WhenNoParent()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        await Assert.That(frame.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task Depth_ReturnsOne_WhenHasParent()
    {
        var parent = new CallFrame(MakeAction("ParentGoal"));
        var child = new CallFrame(MakeAction("ChildGoal"), parent: parent);

        await Assert.That(child.Depth).IsEqualTo(1);
    }

    [Test]
    public async Task Depth_ReturnsTwoForGrandchild()
    {
        var grandparent = new CallFrame(MakeAction("GrandparentGoal"));
        var parent = new CallFrame(MakeAction("ParentGoal"), parent: grandparent);
        var child = new CallFrame(MakeAction("ChildGoal"), parent: parent);

        await Assert.That(child.Depth).IsEqualTo(2);
    }

    [Test]
    public async Task GetStackTrace_IncludesGoalName()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("TestGoal");
    }

    [Test]
    public async Task GetStackTrace_IncludesStepIndex()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        frame.Action.Step = new Step { Index = 5, Text = "test step", LineNumber = 6 };

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("step 6");
    }

    [Test]
    public async Task GetStackTrace_IncludesGoalPath()
    {
        var frame = new CallFrame(MakeAction("TestGoal", "/path/to/goal.pr"));

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("/path/to/goal.pr");
    }

    [Test]
    public async Task GetStackTrace_IncludesDuration()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        await Task.Delay(10);

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("ms");
    }

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var frame = new CallFrame(MakeAction("TestGoal"));
        frame.Phase = ExecutionPhase.ExecutingStep;

        var str = frame.ToString();

        await Assert.That(str).Contains(frame.Id);
        await Assert.That(str).Contains("TestGoal");
        await Assert.That(str).Contains("ExecutingStep");
    }
}
