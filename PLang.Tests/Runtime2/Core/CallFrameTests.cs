using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;

namespace PLang.Tests.Runtime2.Core;

public class CallFrameTests
{
    [Test]
    public async Task Constructor_SetsGoal()
    {
        var goal = new Goal { Name = "TestGoal" };
        var frame = new CallFrame(goal);

        await Assert.That(frame.Goal).IsEqualTo(goal);
        await Assert.That(frame.Goal.Name).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Constructor_SetsGoalWithPath()
    {
        var goal = new Goal { Name = "TestGoal", Path = "/path/to/goal.pr" };
        var frame = new CallFrame(goal);

        await Assert.That(frame.Goal.Path).IsEqualTo("/path/to/goal.pr");
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.Id).IsNotNull();
        await Assert.That(frame.Id.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Constructor_SetsStartedAt()
    {
        var before = DateTime.UtcNow;

        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        var after = DateTime.UtcNow;
        await Assert.That(frame.StartedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(frame.StartedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_DefaultsPhaseToNone()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.None);
    }

    [Test]
    public async Task Constructor_DefaultsStepToNull()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.Step).IsNull();
    }

    [Test]
    public async Task Constructor_SetsParent()
    {
        var parent = new CallFrame(new Goal { Name = "ParentGoal" });
        var child = new CallFrame(new Goal { Name = "ChildGoal" }, parent: parent);

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task Constructor_SetsIndent()
    {
        var parent = new CallFrame(new Goal { Name = "ParentGoal" });
        var child = new CallFrame(new Goal { Name = "ChildGoal" }, parent: parent);

        await Assert.That(parent.Indent).IsEqualTo(0);
        await Assert.That(child.Indent).IsEqualTo(1);
    }

    [Test]
    public async Task CompletedAt_DefaultsToNull()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.CompletedAt).IsNull();
    }

    [Test]
    public async Task Errors_DefaultsToEmpty()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Phase_CanBeSet()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        frame.Phase = ExecutionPhase.ExecutingGoal;

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.ExecutingGoal);
    }

    [Test]
    public async Task Step_CanBeSet()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        var step = new Step { Index = 5, Text = "call http endpoint", LineNumber = 6 };

        frame.Step = step;

        await Assert.That(frame.Step).IsEqualTo(step);
        await Assert.That(frame.Step!.Index).IsEqualTo(5);
        await Assert.That(frame.Step!.Text).IsEqualTo("call http endpoint");
    }

    [Test]
    public async Task EventId_CanBeSet()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        frame.EventId = "event123";

        await Assert.That(frame.EventId).IsEqualTo("event123");
    }

    [Test]
    public async Task Duration_ReturnsPositiveTimeSpan()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        await Task.Delay(10);

        var duration = frame.Duration;

        await Assert.That(duration.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task RecordStep_AddsStep()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        var step = new Step { Index = 0, Text = "first step" };

        frame.RecordStep(step);

        await Assert.That(frame.ExecutedSteps.Count).IsEqualTo(1);
        await Assert.That(frame.ExecutedSteps[0].Step.Index).IsEqualTo(0);
        await Assert.That(frame.ExecutedSteps[0].Step.Text).IsEqualTo("first step");
    }

    [Test]
    public async Task RecordStep_SetsStartedAt()
    {
        var before = DateTime.UtcNow;

        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.RecordStep(new Step { Index = 0, Text = "step" });

        var after = DateTime.UtcNow;
        await Assert.That(frame.ExecutedSteps[0].StartedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(frame.ExecutedSteps[0].StartedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task RecordStep_RespectsMaxStepsLimit()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        for (int i = 0; i <= CallFrame.MaxStepsPerFrame; i++)
        {
            frame.RecordStep(new Step { Index = i, Text = $"step {i}" });
        }

        await Assert.That(frame.ExecutedSteps.Count).IsEqualTo(CallFrame.MaxStepsPerFrame);
    }

    [Test]
    public async Task CompleteCurrentStep_SetsCompletedAt()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.RecordStep(new Step { Index = 0, Text = "step" });

        frame.CompleteCurrentStep();

        await Assert.That(frame.ExecutedSteps[0].CompletedAt).IsNotNull();
    }

    [Test]
    public async Task CompleteCurrentStep_SetsDuration()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.RecordStep(new Step { Index = 0, Text = "step" });
        await Task.Delay(10);

        frame.CompleteCurrentStep();

        await Assert.That(frame.ExecutedSteps[0].Duration).IsNotNull();
        await Assert.That(frame.ExecutedSteps[0].Duration!.Value.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task CompleteCurrentStep_WithExplicitDuration_UsesDuration()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.RecordStep(new Step { Index = 0, Text = "step" });

        frame.CompleteCurrentStep(TimeSpan.FromMilliseconds(100));

        await Assert.That(frame.ExecutedSteps[0].Duration!.Value.TotalMilliseconds).IsEqualTo(100);
    }

    [Test]
    public async Task CompleteCurrentStep_NoSteps_DoesNotThrow()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        frame.CompleteCurrentStep();

        await Assert.That(frame.ExecutedSteps.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_SetsCompletedAt()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        frame.Complete();

        await Assert.That(frame.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Complete_SetsPhaseToNone_WhenNoErrors()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        frame.Complete();

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.None);
    }

    [Test]
    public async Task Complete_SetsPhaseToError_WhenHasErrors()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.AddError(new Error("Test error"));

        frame.Complete();

        await Assert.That(frame.Phase).IsEqualTo(ExecutionPhase.Error);
    }

    [Test]
    public async Task AddError_AddsToErrorsList()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        var error = new Error("Test error");

        frame.AddError(error);

        await Assert.That(frame.Errors.Count).IsEqualTo(1);
        await Assert.That(frame.Errors[0]).IsEqualTo(error);
    }

    [Test]
    public async Task IsInEvent_ReturnsFalse_WhenNoEventId()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.IsInEvent).IsFalse();
    }

    [Test]
    public async Task IsInEvent_ReturnsTrue_WhenHasEventId()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.EventId = "event123";

        await Assert.That(frame.IsInEvent).IsTrue();
    }

    [Test]
    public async Task IsInEvent_ReturnsTrue_WhenParentIsInEvent()
    {
        var parent = new CallFrame(new Goal { Name = "ParentGoal" });
        parent.EventId = "event123";
        var child = new CallFrame(new Goal { Name = "ChildGoal" }, parent: parent);

        await Assert.That(child.IsInEvent).IsTrue();
    }

    [Test]
    public async Task Depth_ReturnsZero_WhenNoParent()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        await Assert.That(frame.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task Depth_ReturnsOne_WhenHasParent()
    {
        var parent = new CallFrame(new Goal { Name = "ParentGoal" });
        var child = new CallFrame(new Goal { Name = "ChildGoal" }, parent: parent);

        await Assert.That(child.Depth).IsEqualTo(1);
    }

    [Test]
    public async Task Depth_ReturnsTwoForGrandchild()
    {
        var grandparent = new CallFrame(new Goal { Name = "GrandparentGoal" });
        var parent = new CallFrame(new Goal { Name = "ParentGoal" }, parent: grandparent);
        var child = new CallFrame(new Goal { Name = "ChildGoal" }, parent: parent);

        await Assert.That(child.Depth).IsEqualTo(2);
    }

    [Test]
    public async Task GetStackTrace_IncludesGoalName()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("TestGoal");
    }

    [Test]
    public async Task GetStackTrace_IncludesStepIndex()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.Step = new Step { Index = 5, Text = "test step", LineNumber = 6 };

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("step 6");
    }

    [Test]
    public async Task GetStackTrace_IncludesGoalPath()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal", Path = "/path/to/goal.pr" });

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("/path/to/goal.pr");
    }

    [Test]
    public async Task GetStackTrace_IncludesDuration()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        await Task.Delay(10);

        var trace = frame.GetStackTrace();

        await Assert.That(trace).Contains("ms");
    }

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var frame = new CallFrame(new Goal { Name = "TestGoal" });
        frame.Phase = ExecutionPhase.ExecutingStep;

        var str = frame.ToString();

        await Assert.That(str).Contains(frame.Id);
        await Assert.That(str).Contains("TestGoal");
        await Assert.That(str).Contains("ExecutingStep");
    }
}

public class ExecutedStepTests
{
    [Test]
    public async Task Properties_StoresStepReference()
    {
        var step = new Step { Index = 5, Text = "test step" };
        var executed = new ExecutedStep(step);

        await Assert.That(executed.Step).IsEqualTo(step);
        await Assert.That(executed.Step.Index).IsEqualTo(5);
        await Assert.That(executed.Step.Text).IsEqualTo("test step");
        await Assert.That(executed.StartedAt).IsNotEqualTo(default(DateTime));
    }

    [Test]
    public async Task CompletedAt_CanBeSet()
    {
        var executed = new ExecutedStep(new Step { Index = 0, Text = "step" });

        executed.CompletedAt = DateTime.UtcNow;

        await Assert.That(executed.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Duration_CanBeSet()
    {
        var executed = new ExecutedStep(new Step { Index = 0, Text = "step" });

        executed.Duration = TimeSpan.FromMilliseconds(100);

        await Assert.That(executed.Duration).IsNotNull();
        await Assert.That(executed.Duration!.Value.TotalMilliseconds).IsEqualTo(100);
    }
}
