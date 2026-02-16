using PLang.Runtime2;
using PLang.Runtime2.Memory;

namespace PLang.Tests.Runtime2;

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
            Actions = new Actions
            {
                new PLang.Runtime2.Action
                {
                    Module = "http",
                    ActionName = "get",
                    Parameters = new List<Data> { new Data("url", "https://api.example.com") },
                    Return = new List<Data> { new Data("response") }
                }
            },
            OnErrorGoal = "HandleError",
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
        await Assert.That(step.OnErrorGoal).IsEqualTo("HandleError");
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
            Actions = new Actions
            {
                new PLang.Runtime2.Action
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data> { new Data("name", "test") },
                    Return = new List<Data> { new Data("result") }
                }
            },
            OnErrorGoal = "ErrorHandler",
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
        await Assert.That(clone.OnErrorGoal).IsEqualTo(original.OnErrorGoal);
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
    public async Task OnErrorGoal_DefaultsToNull()
    {
        var step = new Step();

        await Assert.That(step.OnErrorGoal).IsNull();
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
