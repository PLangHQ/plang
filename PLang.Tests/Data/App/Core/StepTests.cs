using app;
using app.variable;

namespace PLang.Tests.App.Core;

public class StepTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/StepTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

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
            Action = new StepActions
            {
                new global::app.goal.step.action.@this
                {
                    Module = "http",
                    ActionName = "get",
                    Parameter = new List<Data> { app.Data("url", "https://api.example.com") },
                }
            },
            WaitForExecution = false
        };

        await Assert.That(step.Index).IsEqualTo(5);
        await Assert.That(step.Text).IsEqualTo("call http endpoint");
        await Assert.That(step.LineNumber).IsEqualTo(10);
        await Assert.That(step.Indent).IsEqualTo(2);
        await Assert.That(step.Comment).IsEqualTo("This makes an HTTP call");
        await Assert.That(step.Action.Count).IsEqualTo(1);
        await Assert.That(step.Action[0].Module).IsEqualTo("http");
        await Assert.That(step.Action[0].ActionName).IsEqualTo("get");
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

        await Assert.That(step.Action).IsNotNull();
        await Assert.That(step.Action.Count).IsEqualTo(0);
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
