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

    [Test]
    public async Task ValidResponse_TwoSteps_ReturnsOk()
    {
        var steps = new Dictionary<string, object?>
        {
            ["steps"] = new List<object>
            {
                new Dictionary<string, object?> { ["index"] = 0, ["actions"] = new List<object> { new Dictionary<string, object?> { ["module"] = "output", ["action"] = "write" } } },
                new Dictionary<string, object?> { ["index"] = 1, ["actions"] = new List<object> { new Dictionary<string, object?> { ["module"] = "variable", ["action"] = "set" } } }
            }
        };

        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", steps),
            Goal = new Data("", MakeGoal(2))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task WrongStepCount_ReturnsError()
    {
        var steps = new Dictionary<string, object?>
        {
            ["steps"] = new List<object>
            {
                new Dictionary<string, object?> { ["index"] = 0, ["actions"] = new List<object> { new Dictionary<string, object?> { ["module"] = "output", ["action"] = "write" } } }
            }
        };

        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", steps),
            Goal = new Data("", MakeGoal(3))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Step count");
        await Assert.That(result.Error!.Message).Contains("returned 1, expected 3");
    }

    [Test]
    public async Task MissingSteps_ReturnsError()
    {
        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", new Dictionary<string, object?> { ["noSteps"] = true }),
            Goal = new Data("", MakeGoal(1))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Could not find 'steps'");
    }

    [Test]
    public async Task MissingIndex_ReturnsError()
    {
        var steps = new Dictionary<string, object?>
        {
            ["steps"] = new List<object>
            {
                new Dictionary<string, object?> { ["actions"] = new List<object> { new Dictionary<string, object?> { ["module"] = "x" } } }
            }
        };

        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", steps),
            Goal = new Data("", MakeGoal(1))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("missing 'index'");
    }

    [Test]
    public async Task NoActions_ReturnsError()
    {
        var steps = new Dictionary<string, object?>
        {
            ["steps"] = new List<object>
            {
                new Dictionary<string, object?> { ["index"] = 0, ["actions"] = new List<object>() }
            }
        };

        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", steps),
            Goal = new Data("", MakeGoal(1))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("no actions");
    }

    [Test]
    public async Task GapInIndexes_ReturnsError()
    {
        var steps = new Dictionary<string, object?>
        {
            ["steps"] = new List<object>
            {
                new Dictionary<string, object?> { ["index"] = 0, ["actions"] = new List<object> { new Dictionary<string, object?> { ["module"] = "x" } } },
                new Dictionary<string, object?> { ["index"] = 2, ["actions"] = new List<object> { new Dictionary<string, object?> { ["module"] = "x" } } }
            }
        };

        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", steps),
            Goal = new Data("", MakeGoal(2))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("indexes must be 0..1");
    }

    [Test]
    public async Task NullInputs_ReturnsError()
    {
        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data(""),
            Goal = new Data("")
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task MultipleErrors_CollectsAll()
    {
        // Wrong count + missing index + no actions = multiple errors
        var steps = new Dictionary<string, object?>
        {
            ["steps"] = new List<object>
            {
                new Dictionary<string, object?> { /* no index, no actions */ }
            }
        };

        var action = new validateResponse
        {
            Context = _app.Context,
            StepResults = new Data("", steps),
            Goal = new Data("", MakeGoal(3))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        // Should contain step count error AND missing index AND no actions
        await Assert.That(result.Error!.Message).Contains("Step count");
        await Assert.That(result.Error!.Message).Contains("missing 'index'");
        await Assert.That(result.Error!.Message).Contains("no actions");
    }
}
