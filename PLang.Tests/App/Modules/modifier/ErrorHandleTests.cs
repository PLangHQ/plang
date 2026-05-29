namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the error.handle modifier handler.
/// Wraps an action with error matching, retry logic, and error goal calls.
/// </summary>
public class ErrorHandleTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static PrAction Throw(string message, int? statusCode = null, string? key = null,
        ActionModifiers? modifiers = null)
    {
        var parameters = new List<global::app.data.@this> { new("message", message) };
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
        var list = new List<global::app.data.@this>();
        foreach (var p in parameters) list.Add(new(p.name, p.value));
        return new PrAction
        {
            Module = "error", ActionName = "handle",
            Parameters = list
        };
    }

    /// <summary>Single-action recovery list that calls <paramref name="goalName"/>.</summary>
    private static List<PrAction> CallGoal(string goalName) => new()
    {
        new PrAction
        {
            Module = "goal", ActionName = "call",
            Parameters = new List<global::app.data.@this>
            {
                new("goalname", new Dictionary<string, object?> { ["name"] = goalName })
            }
        }
    };

    [Test]
    public async Task Handle_ActionSucceeds_PassesThrough()
    {
        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%ok%"), new("value", "v")
            },
            Modifiers = new ActionModifiers { ErrorHandler(("ignoreError", true)) }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variable.GetValue("ok")).IsEqualTo("v");
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
        // RetryCount=2, no goal, persistent failure → retries exhaust, error propagates.
        // Stateful lambda counts calls so a regression in the retry loop fails this test.
        int callCount = 0;
        Func<Task<global::app.data.@this>> persistentlyFailing = () =>
        {
            callCount++;
            return Task.FromResult(global::app.data.@this.FromError(
                new global::app.error.ServiceError("persistent failure", "TransientError", 503)));
        };

        var modifiers = new ActionModifiers
        {
            ErrorHandler(("retryCount", 2), ("order", "RetryFirst"))
        };

        var result = await modifiers.RunAsync(persistentlyFailing, Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).IsEqualTo("persistent failure");
        await Assert.That(callCount).IsEqualTo(3); // 1 initial + 2 retries
    }

    [Test]
    public async Task Handle_GoalFirst_NoGoal_ExhaustsRetriesAndFails()
    {
        // Order=GoalFirst, no goal + retryCount=1, persistent failure → 1 initial + 1 retry.
        int callCount = 0;
        Func<Task<global::app.data.@this>> persistentlyFailing = () =>
        {
            callCount++;
            return Task.FromResult(global::app.data.@this.FromError(
                new global::app.error.ServiceError("failure", "TransientError", 503)));
        };

        var modifiers = new ActionModifiers
        {
            ErrorHandler(("retryCount", 1), ("order", "GoalFirst"))
        };

        var result = await modifiers.RunAsync(persistentlyFailing, Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(callCount).IsEqualTo(2); // 1 initial + 1 retry
    }

    [Test]
    public async Task Handle_RetryFirst_PersistentFailure_AllRetriesFail()
    {
        // RetryCount=3, persistent failure → 1 initial + 3 retries = 4 calls total.
        int callCount = 0;
        Func<Task<global::app.data.@this>> persistentlyFailing = () =>
        {
            callCount++;
            return Task.FromResult(global::app.data.@this.FromError(
                new global::app.error.ServiceError("always fails", "TransientError", 503)));
        };

        var modifiers = new ActionModifiers { ErrorHandler(("retryCount", 3)) };

        var result = await modifiers.RunAsync(persistentlyFailing, Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(callCount).IsEqualTo(4); // 1 initial + 3 retries
    }

    [Test]
    public async Task Handle_RetrySucceedsOnSecondAttempt_ReturnsSuccess()
    {
        // Stateful lambda: fails on first call, succeeds on second.
        // Tests the retry-success path that error.throw can't cover.
        int callCount = 0;
        Func<Task<global::app.data.@this>> statefulNext = () =>
        {
            callCount++;
            if (callCount == 1)
                return Task.FromResult(global::app.data.@this.FromError(
                    new global::app.error.ServiceError("transient failure", "TransientError", 503)));
            return Task.FromResult(global::app.data.@this.Ok());
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
    /// Creates an in-memory goal with a single action step and registers it in app.goal.
    /// </summary>
    private Goal RegisterGoal(string name, string module, string actionName,
        params (string name, object? value)[] parameters)
    {
        var prAction = new PrAction
        {
            Module = module, ActionName = actionName,
            Parameters = parameters.Select(p => new global::app.data.@this(p.name, p.value)).ToList()
        };
        var step = new Step { Text = $"test step for {name}" };
        step.Actions.Add(prAction);
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        goal.Steps.Add(step);
        _app.Goal.Add(goal);
        return goal;
    }

    [Test]
    public async Task Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult()
    {
        RegisterGoal("SuccessGoal", "variable", "set", ("name", "%marker%"), ("value", "handled"));

        var action = Throw("boom",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("actions", CallGoal("SuccessGoal")), ("order", "GoalFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_GoalFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal", "error", "throw", ("message", "goal failed"));

        var action = Throw("original error",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("actions", CallGoal("FailGoal")), ("order", "GoalFirst"))
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

        var action = Throw("persistent",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("actions", CallGoal("SuccessGoal2")), ("order", "RetryFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Handle_RetryFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal2", "error", "throw", ("message", "goal also failed"));

        var action = Throw("persistent",
            modifiers: new ActionModifiers
            {
                ErrorHandler(("actions", CallGoal("FailGoal2")), ("order", "RetryFirst"))
            });

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.ErrorChain.Count).IsGreaterThan(0);
        await Assert.That(result.Error.ErrorChain[0].Message).IsEqualTo("goal also failed");
    }
}
