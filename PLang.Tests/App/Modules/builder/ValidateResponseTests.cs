using global::App.modules.builder;

namespace PLang.Tests.App.Modules.builder;

public class ValidateResponseTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    private Goal MakeGoal(int stepCount)
    {
        var goal = new Goal { Name = "TestGoal", Path = "/Test.goal" };
        for (int i = 0; i < stepCount; i++)
            goal.Steps.Add(new Step { Index = i, Text = $"step {i}" });
        return goal;
    }

    private Goal MakeGoalWithPriorActions(int stepCount)
    {
        var goal = new Goal { Name = "TestGoal", Path = "/Test.goal" };
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
        global::App.@this app)
    {
        return new validateResponse
        {
            Context = app.Context,
            StepResults = new global::App.Data.@this<BuildResponse>("", response),
            Goal = new global::App.Data.@this<Goal>("", goal)
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

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task WrongStepCount_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { BuildStep(0, ("output", "write")) }
        };
        var result = await Make(response, MakeGoal(3), _app).Run();

        await Assert.That(result.Success).IsFalse();
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

        await Assert.That(result.Success).IsFalse();
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

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("indexes must be 0..1");
    }

    [Test]
    public async Task NullInputs_ReturnsError()
    {
        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new global::App.Data.@this<BuildResponse>(),
            Goal = new global::App.Data.@this<Goal>(),
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task KeepTrue_NoActionsEmitted_PriorHasActions_ReturnsOk()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0, Keep = true } }
        };
        var result = await Make(response, MakeGoalWithPriorActions(1), _app).Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task KeepTrue_PriorHasNoActions_ReturnsError()
    {
        var response = new BuildResponse
        {
            Steps = new() { new Step { Index = 0, Keep = true } }
        };
        var result = await Make(response, MakeGoal(1), _app).Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("keep:true but the prior .pr has no actions");
    }

    // --- Scalar PlangType shape check ---
    // tstring/path are Scalar PlangTypes — wire form is bare string.
    // The LLM sometimes wraps tstring values as `{value, key}`; this catches that
    // so LlmFixer retries with the error feedback.

    [Test]
    public async Task TstringParam_StringValue_ReturnsOk()
    {
        var step = new Step { Index = 0 };
        var act = new PrAction { Module = "output", ActionName = "write" };
        act.Parameters.Add(new Data("Data", "Hello %name%", new global::App.Data.Type("tstring")));
        step.Actions.Add(act);

        var response = new BuildResponse { Steps = new() { step } };
        var result = await Make(response, MakeGoal(1), _app).Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task TstringParam_RecordValue_ReturnsError()
    {
        var step = new Step { Index = 0 };
        var act = new PrAction { Module = "output", ActionName = "write" };
        var record = new Dictionary<string, object?>
        {
            ["value"] = "Hello %name%",
            ["key"] = null,
        };
        act.Parameters.Add(new Data("Data", record, new global::App.Data.Type("tstring")));
        step.Actions.Add(act);

        var response = new BuildResponse { Steps = new() { step } };
        var result = await Make(response, MakeGoal(1), _app).Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("type 'tstring' but value is not a plain string");
        await Assert.That(result.Error!.Message).Contains("bare string values");
    }

    [Test]
    public async Task PathParam_RecordValue_ReturnsError()
    {
        var step = new Step { Index = 0 };
        var act = new PrAction { Module = "file", ActionName = "read" };
        var record = new Dictionary<string, object?>
        {
            ["raw"] = "/tmp/x.txt",
            ["absolute"] = "/tmp/x.txt",
        };
        act.Parameters.Add(new Data("Path", record, new global::App.Data.Type("path")));
        step.Actions.Add(act);

        var response = new BuildResponse { Steps = new() { step } };
        var result = await Make(response, MakeGoal(1), _app).Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("type 'path' but value is not a plain string");
    }

    [Test]
    public async Task ScalarShape_KeepTrueStep_Skipped()
    {
        // keep:true reuses prior; the inbound bad shape is irrelevant — enrichResponse
        // will overwrite from prior. Don't double-error.
        var step = new Step { Index = 0, Keep = true };
        var act = new PrAction { Module = "output", ActionName = "write" };
        act.Parameters.Add(new Data("Data",
            new Dictionary<string, object?> { ["value"] = "x" },
            new global::App.Data.Type("tstring")));
        step.Actions.Add(act);

        var response = new BuildResponse { Steps = new() { step } };
        var result = await Make(response, MakeGoalWithPriorActions(1), _app).Run();

        await Assert.That(result.Success).IsTrue();
    }
}
