using App;
using global::App.Variables;

namespace PLang.Tests.App.Core;

public class StepTests
{
    [Test]
    public async Task Properties_CanBeInitialized()
    {
        var step = new Step
        {
            Index = 5,
            Text = "call http endpoint",
            LineNumber = 10,
            Indent = 2,
            Comment = "This makes an HTTP call",
            Actions = new StepActions
            {
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = "http",
                    ActionName = "get",
                    Parameters = new List<Data> { new Data("url", "https://api.example.com") },
                    Return = new List<Data> { new Data("response") }
                }
            },
            WaitForExecution = false
        };

        await Assert.That(step.Index).IsEqualTo(5);
        await Assert.That(step.Text).IsEqualTo("call http endpoint");
        await Assert.That(step.LineNumber).IsEqualTo(10);
        await Assert.That(step.Indent).IsEqualTo(2);
        await Assert.That(step.Comment).IsEqualTo("This makes an HTTP call");
        await Assert.That(step.Actions.Count).IsEqualTo(1);
        await Assert.That(step.Actions[0].Module).IsEqualTo("http");
        await Assert.That(step.Actions[0].ActionName).IsEqualTo("get");
        await Assert.That(step.WaitForExecution).IsFalse();
    }

    [Test]
    public async Task WaitForExecution_DefaultsToTrue()
    {
        var step = new Step();

        await Assert.That(step.WaitForExecution).IsTrue();
    }

    [Test]
    public async Task Actions_DefaultsToEmptyList()
    {
        var step = new Step();

        await Assert.That(step.Actions).IsNotNull();
        await Assert.That(step.Actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Goal_CanBeSet()
    {
        var step = new Step();
        var goal = new Goal { Name = "TestGoal" };

        step.Goal = goal;

        await Assert.That(step.Goal).IsEqualTo(goal);
    }

    // --- HasSubSteps ---

    [Test]
    public async Task HasSubSteps_NoGoal_DefaultsFalse()
    {
        var step = new Step { Index = 0, Indent = 0 };

        await Assert.That(step.HasSubSteps).IsFalse();
    }

    [Test]
    public async Task HasSubSteps_LazyFromGoalSteps()
    {
        var goal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "if %flag%", Indent = 0 },
                new Step { Index = 1, Text = "set %x% = 1", Indent = 1 },
                new Step { Index = 2, Text = "write out done", Indent = 0 }
            }
        };
        foreach (var s in goal.Steps) s.Goal = goal;

        await Assert.That(goal.Steps[0].HasSubSteps).IsTrue();
        await Assert.That(goal.Steps[1].HasSubSteps).IsFalse();
        await Assert.That(goal.Steps[2].HasSubSteps).IsFalse();
    }

    [Test]
    public async Task HasSubSteps_NestedConditions()
    {
        var goal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step { Index = 0, Text = "if %x% > 5", Indent = 0 },
                new Step { Index = 1, Text = "if %y% > 100", Indent = 1 },
                new Step { Index = 2, Text = "set %inner% = yes", Indent = 2 },
                new Step { Index = 3, Text = "set %outer% = yes", Indent = 1 },
                new Step { Index = 4, Text = "write out done", Indent = 0 }
            }
        };
        foreach (var s in goal.Steps) s.Goal = goal;

        await Assert.That(goal.Steps[0].HasSubSteps).IsTrue();
        await Assert.That(goal.Steps[1].HasSubSteps).IsTrue();
        await Assert.That(goal.Steps[2].HasSubSteps).IsFalse();
        await Assert.That(goal.Steps[3].HasSubSteps).IsFalse();
        await Assert.That(goal.Steps[4].HasSubSteps).IsFalse();
    }

    [Test]
    public async Task Clone_CreatesDeepCopy()
    {
        var originalGoal = new Goal { Name = "TestGoal" };
        var original = new Step
        {
            Index = 1,
            Text = "original text",
            LineNumber = 5,
            Indent = 1,
            Comment = "original comment",
            Actions = new StepActions
            {
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data> { new Data("name", "test") },
                    Return = new List<Data> { new Data("result") }
                }
            },
            WaitForExecution = false,
            Goal = originalGoal
        };

        var clone = original.Clone();

        await Assert.That(clone.Index).IsEqualTo(original.Index);
        await Assert.That(clone.Text).IsEqualTo(original.Text);
        await Assert.That(clone.LineNumber).IsEqualTo(original.LineNumber);
        await Assert.That(clone.Indent).IsEqualTo(original.Indent);
        await Assert.That(clone.Comment).IsEqualTo(original.Comment);
        await Assert.That(clone.Actions.Count).IsEqualTo(1);
        await Assert.That(clone.Actions[0].Module).IsEqualTo("variable");
        await Assert.That(clone.Actions[0].ActionName).IsEqualTo("set");
        await Assert.That(clone.WaitForExecution).IsEqualTo(original.WaitForExecution);
        await Assert.That(clone.Goal).IsEqualTo(original.Goal);
    }

    [Test]
    public async Task Clone_IsNotSameReference()
    {
        var original = new Step { Index = 1, Text = "test" };

        var clone = original.Clone();

        await Assert.That(clone).IsNotEqualTo(original);
    }

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var step = new Step { Index = 5, Text = "call http endpoint" };

        var str = step.ToString();

        await Assert.That(str).IsEqualTo("[5] call http endpoint");
    }

    [Test]
    public async Task ToString_EmptyText_ReturnsEmptyBrackets()
    {
        var step = new Step { Index = 0 };

        var str = step.ToString();

        await Assert.That(str).IsEqualTo("[0] ");
    }

    [Test]
    public async Task Text_DefaultsToEmptyString()
    {
        var step = new Step();

        await Assert.That(step.Text).IsEqualTo("");
    }

    [Test]
    public async Task Comment_DefaultsToNull()
    {
        var step = new Step();

        await Assert.That(step.Comment).IsNull();
    }

    [Test]
    public async Task Timeout_DefaultsToNull()
    {
        var step = new Step();

        await Assert.That(step.Timeout).IsNull();
    }

    [Test]
    public async Task Errors_DefaultsToEmptyList()
    {
        var step = new Step();

        await Assert.That(step.Errors).IsNotNull();
        await Assert.That(step.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Warnings_DefaultsToEmptyList()
    {
        var step = new Step();

        await Assert.That(step.Warnings).IsNotNull();
        await Assert.That(step.Warnings.Count).IsEqualTo(0);
    }
}
