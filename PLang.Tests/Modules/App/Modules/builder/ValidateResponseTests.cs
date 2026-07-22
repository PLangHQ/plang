using app.module.action.build;

namespace PLang.Tests.App.Modules.builder;

public class ValidateResponseTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    private Goal MakeGoal(int stepCount)
    {
        var goal = new Goal { Name = "TestGoal", Path = global::app.type.item.path.@this.Resolve("/Test.goal", global::PLang.Tests.TestApp.SharedContext) };
        for (int i = 0; i < stepCount; i++)
            goal.Step.Add(new Step { Index = i, Text = $"step {i}" });
        return goal;
    }

    private Goal MakeGoalWithPriorActions(int stepCount)
    {
        var goal = new Goal { Name = "TestGoal", Path = global::app.type.item.path.@this.Resolve("/Test.goal", global::PLang.Tests.TestApp.SharedContext) };
        for (int i = 0; i < stepCount; i++)
        {
            var step = new Step { Index = i, Text = $"step {i}" };
            step.Action.Add(new PrAction { Module = "output", ActionName = "write" });
            goal.Step.Add(step);
        }
        return goal;
    }

    private static Step BuildStep(int index, params (string module, string action)[] actions)
    {
        var s = new Step { Index = index };
        foreach (var (m, a) in actions)
            s.Action.Add(new PrAction { Module = m, ActionName = a });
        return s;
    }

    private static Task<global::app.data.@this> Validate(BuildResponse response, Goal goal,
        global::app.@this app)
        => response.Validate(goal, app);

    [Test]
    public async Task ValidResponse_TwoSteps_ReturnsOk()
    {
        var response = new BuildResponse
        {
            Steps = new()
            {
                BuildStep(0, ("output", "write")),
                BuildStep(1, ("variable", "set")),
            }
        };
        var result = await Validate(response, MakeGoal(2), _app);

        await result.IsSuccess();
    }

    [Test]
    public async Task WrongStepCount_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { BuildStep(0, ("output", "write")) }
        };
        var result = await Validate(response, MakeGoal(3), _app);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("Step count");
        await Assert.That(result.Error!.Message).Contains("returned 1, expected 3");
    }

    [Test]
    public async Task NoActions_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0 } }
        };
        var result = await Validate(response, MakeGoal(1), _app);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("no actions");
    }

    [Test]
    public async Task GapInIndexes_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new()
            {
                BuildStep(0, ("output", "write")),
                BuildStep(2, ("output", "write")),
            }
        };
        var result = await Validate(response, MakeGoal(2), _app);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("indexes must be 0..1");
    }

    [Test]
    public async Task KeepTrue_NoActionsEmitted_PriorHasActions_ReturnsOk()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0, Keep = true } }
        };
        var result = await Validate(response, MakeGoalWithPriorActions(1), _app);

        await result.IsSuccess();
    }

    [Test]
    public async Task KeepTrue_PriorHasNoActions_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0, Keep = true } }
        };
        var result = await Validate(response, MakeGoal(1), _app);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("keep:true but the prior .pr has no actions");
    }

}
