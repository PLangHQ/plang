using app.module.environment;
using app.goal;

namespace PLang.Tests.App.Modules.app;

public class AppRunTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").TestSigning();
        _app.goal.list.Add(new global::app.goal.@this
        {
            Name = "RunTarget",
            Path = global::app.type.item.path.@this.Resolve("/RunTarget.goal", _app.actor.list.User.Context)
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Run_GoalCall_ResolvesAndRuns()
    {
        var action = new start(_app.actor.list.User.Context) { Goal = Make.Call("RunTarget") };
        var result = await action.Start();

        await result.IsSuccess();
    }

    [Test]
    public async Task Run_MissingGoal_ReturnsError()
    {
        var action = new start(_app.actor.list.User.Context) { Goal = Make.Call("DoesNotExist") };
        var result = await action.Start();

        await result.IsFailure();
    }

    [Test]
    public async Task Run_Step_ExecutesStep()
    {
        var step = new global::app.goal.step.@this
        {
            Text = "test step",
            Index = 0
        };
        var action = new start(_app.actor.list.User.Context) { Step = new("", step, context: _app.actor.list.User.Context)
        };
        var result = await action.Start();

        // Step with no actions returns Ok
        await result.IsSuccess();
    }

    [Test]
    public async Task Run_NoInput_ReturnsError()
    {
        var action = new start(_app.actor.list.User.Context) { Goal = null,
            Step = null,
            Action = null
        };
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingInput");
    }
}
