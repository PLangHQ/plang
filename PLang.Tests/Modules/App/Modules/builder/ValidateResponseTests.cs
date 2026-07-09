using app.module.build;

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
        var goal = new Goal { Name = "TestGoal", Path = global::app.type.path.@this.Resolve("/Test.goal", global::PLang.Tests.TestApp.SharedContext) };
        for (int i = 0; i < stepCount; i++)
            goal.Steps.Add(new Step { Index = i, Text = $"step {i}" });
        return goal;
    }

    private Goal MakeGoalWithPriorActions(int stepCount)
    {
        var goal = new Goal { Name = "TestGoal", Path = global::app.type.path.@this.Resolve("/Test.goal", global::PLang.Tests.TestApp.SharedContext) };
        for (int i = 0; i < stepCount; i++)
        {
            var step = new Step { Index = i, Text = $"step {i}" };
            step.Actions.Add(new PrAction { Module = "output", ActionName = "write" });
            goal.Steps.Add(step);
        }
        return goal;
    }

    private static Step BuildStep(int index, params (string module, string action)[] actions)
    {
        var s = new Step { Index = index };
        foreach (var (m, a) in actions)
            s.Actions.Add(new PrAction { Module = m, ActionName = a });
        return s;
    }

    private static validateResponse Make(BuildResponse response, Goal goal,
        global::app.@this app)
    {
        return new validateResponse(app.User.Context) { StepResults = new global::app.data.@this<BuildResponse>("", response),
            Goal = new global::app.data.@this<global::app.type.clr.@this<Goal>>("", new global::app.type.clr.@this<Goal>(goal, app.User.Context))
        };
    }

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
        var result = await Make(response, MakeGoal(2), _app).Run();

        await result.IsSuccess();
    }

    [Test]
    public async Task WrongStepCount_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { BuildStep(0, ("output", "write")) }
        };
        var result = await Make(response, MakeGoal(3), _app).Run();

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
        var result = await Make(response, MakeGoal(1), _app).Run();

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
        var result = await Make(response, MakeGoal(2), _app).Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("indexes must be 0..1");
    }

    [Test]
    public async Task NullInputs_ReturnsError()
    {
        var action = new validateResponse(_app.User.Context) { StepResults = new global::app.data.@this<BuildResponse>(),
            Goal = new global::app.data.@this<global::app.type.clr.@this<Goal>>(),
        };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
        // Pin which validation fired — multiple ValidationError variants exist
        // (null inputs, step-count mismatch, gap in indexes, Keep-without-prior).
        // The empty Data<T>() ctor produces an initialized-but-null wrapper, so
        // both branches of the null-input message report each parameter.
        await Assert.That(result.Error!.Message).Contains("StepResults.Value is null");
        await Assert.That(result.Error!.Message).Contains("Goal.Value is null");
    }

    [Test]
    public async Task KeepTrue_NoActionsEmitted_PriorHasActions_ReturnsOk()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0, Keep = true } }
        };
        var result = await Make(response, MakeGoalWithPriorActions(1), _app).Run();

        await result.IsSuccess();
    }

    [Test]
    public async Task KeepTrue_PriorHasNoActions_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0, Keep = true } }
        };
        var result = await Make(response, MakeGoal(1), _app).Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("keep:true but the prior .pr has no actions");
    }

}
