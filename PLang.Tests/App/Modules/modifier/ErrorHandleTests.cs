namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the error.handle modifier handler.
/// Wraps an action with error matching, retry logic, and error goal calls.
/// </summary>
public class ErrorHandleTests
{
    private global::App.@this _app = null!;
    private global::App.Actor.Context.@this Ctx => _app.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static PrAction Throw(string message, int? statusCode = null, string? key = null,
        ActionModifiers? modifiers = null)
    {
        var parameters = new List<global::App.Data.@this> { new("message", message) };
        if (statusCode != null) parameters.Add(new("statusCode", statusCode.Value));
        if (key != null) parameters.Add(new("key", key));
        return new PrAction
        {
            Module = "error", ActionName = "throw",
            Parameters = parameters,
            Modifiers = modifiers ?? new ActionModifiers()
        };
    }

    private static PrAction ErrorHandler(params (string name, object? value)[] parameters)
    {
        var list = new List<global::App.Data.@this>();
        foreach (var p in parameters) list.Add(new(p.name, p.value));
        return new PrAction
        {
            Module = "error", ActionName = "handle",
            Parameters = list
        };
    }

    [Test]
    public async Task Handle_ActionSucceeds_PassesThrough()
    {
        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::App.Data.@this>
            {
                new("name", "%ok%"), new("value", "v")
            },
            Modifiers = new ActionModifiers { ErrorHandler(("ignoreError", true)) }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("ok")).IsEqualTo("v");
    }

    [Test]
    public async Task Handle_IgnoreError_SwallowsErrorReturnsOk()
    {
        var action = Throw("boom",
            modifiers: new ActionModifiers { ErrorHandler(("ignoreError", true)) });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_FilterByStatusCode_MatchHandles()
    {
        var action = Throw("not found", statusCode: 404,
            modifiers: new ActionModifiers
            {
                ErrorHandler(("statusCode", 404), ("ignoreError", true))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_FilterByStatusCode_NoMatchPropagates()
    {
        var action = Throw("server error", statusCode: 500,
            modifiers: new ActionModifiers
            {
                ErrorHandler(("statusCode", 404), ("ignoreError", true))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Handle_FilterByKey_CaseInsensitiveMatch()
    {
        var action = Throw("broken", key: "NotFound",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("key", "notfound"), ("ignoreError", true))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_FilterByMessage_SubstringMatch()
    {
        var action = Throw("connection refused on port 443",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("message", "connection"), ("ignoreError", true))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_FilterByKey_Mismatch_PropagatesError()
    {
        var action = Throw("broken", key: "Timeout",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("key", "NotFound"), ("ignoreError", true))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task Handle_FilterByMessage_Mismatch_PropagatesError()
    {
        var action = Throw("disk full",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("message", "connection"), ("ignoreError", true))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("disk full");
    }

    [Test]
    public async Task Handle_NoFilter_MatchesAllErrors()
    {
        var action = Throw("anything", statusCode: 418,
            modifiers: new ActionModifiers { ErrorHandler(("ignoreError", true)) });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_RetryFirst_NoGoal_ExhaustsRetriesAndFails()
    {
        // error.throw always fails. With RetryCount=2 + no goal, retries exhaust and error propagates.
        var action = Throw("persistent failure",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("retryCount", 2), ("order", "RetryFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("persistent failure");
    }

    [Test]
    public async Task Handle_GoalFirst_NoGoal_ExhaustsRetriesAndFails()
    {
        // Order="GoalFirst" parses as the enum. No goal + retries fail → error propagates.
        var action = Throw("failure",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("retryCount", 1), ("order", "GoalFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Handle_RetryFirst_PersistentFailure_AllRetriesFail()
    {
        // error.throw is deterministic — always fails. This verifies that with
        // RetryCount > 0 and persistent failure, the final result is still failure.
        var action = Throw("always fails",
            modifiers: new ActionModifiers { ErrorHandler(("retryCount", 3)) });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess()
    {
        // Stateful lambda: fails on first call, succeeds on second.
        // Tests the retry-success path that error.throw can't cover.
        int callCount = 0;
        Func<Task<global::App.Data.@this>> statefulNext = () =>
        {
            callCount++;
            if (callCount == 1)
                return Task.FromResult(global::App.Data.@this.FromError(
                    new global::App.Errors.ServiceError("transient failure", "TransientError", 503)));
            return Task.FromResult(global::App.Data.@this.Ok());
        };

        var modifiers = new ActionModifiers
        {
            ErrorHandler(("retryCount", 3))
        };

        var result = await modifiers.RunAsync(statefulNext, Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(callCount).IsEqualTo(2);
    }

    // --- Goal path tests (CallErrorGoal coverage) ---

    /// <summary>
    /// Creates an in-memory goal with a single action step and registers it in app.Goals.
    /// </summary>
    private Goal RegisterGoal(string name, string module, string actionName,
        params (string name, object? value)[] parameters)
    {
        var prAction = new PrAction
        {
            Module = module, ActionName = actionName,
            Parameters = parameters.Select(p => new global::App.Data.@this(p.name, p.value)).ToList()
        };
        var step = new Step { Text = $"test step for {name}" };
        step.Actions.Add(prAction);
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        goal.Steps.Add(step);
        _app.Goals.Add(goal);
        return goal;
    }

    [Test]
    public async Task Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult()
    {
        RegisterGoal("SuccessGoal", "variable", "set", ("name", "%marker%"), ("value", "handled"));

        var goalCall = new GoalCall { Name = "SuccessGoal" };
        var action = Throw("boom",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("goal", goalCall), ("order", "GoalFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_GoalFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal", "error", "throw", ("message", "goal failed"));

        var goalCall = new GoalCall { Name = "FailGoal" };
        var action = Throw("original error",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("goal", goalCall), ("order", "GoalFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.ErrorChain.Count).IsGreaterThan(0);
        await Assert.That(result.Error.ErrorChain[0].Message).IsEqualTo("goal failed");
    }

    [Test]
    public async Task Handle_RetryFirst_GoalSucceeds_ReturnsOk()
    {
        RegisterGoal("SuccessGoal2", "variable", "set", ("name", "%marker2%"), ("value", "ok"));

        var goalCall = new GoalCall { Name = "SuccessGoal2" };
        var action = Throw("persistent",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("goal", goalCall), ("order", "RetryFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_RetryFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal2", "error", "throw", ("message", "goal also failed"));

        var goalCall = new GoalCall { Name = "FailGoal2" };
        var action = Throw("persistent",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("goal", goalCall), ("order", "RetryFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.ErrorChain.Count).IsGreaterThan(0);
        await Assert.That(result.Error.ErrorChain[0].Message).IsEqualTo("goal also failed");
    }

    [Test]
    public async Task Handle_CallErrorGoal_InjectsErrorParameter()
    {
        RegisterGoal("InspectGoal", "variable", "set", ("name", "%marker3%"), ("value", "inspected"));

        var goalCall = new GoalCall { Name = "InspectGoal" };
        var action = Throw("injected error",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("goal", goalCall), ("order", "GoalFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        // CallErrorGoal injects !error into the goalCall parameters, which RunGoalAsync puts into Variables
        var errorParam = Ctx.Variables.GetValue("!error");
        await Assert.That(errorParam).IsNotNull();
    }

    [Test]
    public async Task Handle_CallErrorGoal_DoesNotMutateOriginalParameters()
    {
        RegisterGoal("ParamGoal", "variable", "set", ("name", "%marker4%"), ("value", "param-test"));

        var originalParams = new List<global::App.Data.@this> { new("extra", "val") };
        var goalCall = new GoalCall { Name = "ParamGoal", Parameters = originalParams };
        var action = Throw("mutation test",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("goal", goalCall), ("order", "GoalFirst"))
            });

        await action.RunAsync(Ctx);

        // The fix creates a new list via LINQ instead of mutating the original.
        // However, goalCall.Parameters is reassigned — so we check the original list reference is intact.
        await Assert.That(originalParams.Count).IsEqualTo(1);
        await Assert.That(originalParams[0].Name).IsEqualTo("extra");
    }
}
