using app.modules.environment;
using app.goals.goal;

namespace PLang.Tests.App.Modules.app;

public class AppRunTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app");
        _app.Goals.Add(new global::app.goals.goal.@this
        {
            Name = "RunTarget",
            Path = "/RunTarget.goal"
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Run_GoalCall_ResolvesAndRuns()
    {
        var action = new run
        {
            Context = _app.User.Context,
            GoalName = new GoalCall { Name = "RunTarget" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Run_MissingGoal_ReturnsError()
    {
        var action = new run
        {
            Context = _app.User.Context,
            GoalName = new GoalCall { Name = "DoesNotExist" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Run_Step_ExecutesStep()
    {
        var step = new global::app.goals.goal.steps.step.@this
        {
            Text = "test step",
            Index = 0
        };
        var action = new run
        {
            Context = _app.User.Context,
            Step = step
        };
        var result = await action.Run();

        // Step with no actions returns Ok
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Run_NoInput_ReturnsError()
    {
        var action = new run
        {
            Context = _app.User.Context,
            GoalName = null,
            Step = null,
            Action = null
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingInput");
    }
}
