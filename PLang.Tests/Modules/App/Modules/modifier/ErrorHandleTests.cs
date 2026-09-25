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
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static PrAction Throw(string message, int? statusCode = null, string? key = null,
        List<global::app.goal.step.action.modifier.@this>? modifiers = null)
    {
        var parameters = new List<global::app.data.@this> { new("message", message, context: global::PLang.Tests.TestApp.SharedContext) };
        if (statusCode != null) parameters.Add(new("statusCode", statusCode.Value, context: global::PLang.Tests.TestApp.SharedContext));
        if (key != null) parameters.Add(new("key", key, context: global::PLang.Tests.TestApp.SharedContext));
        return new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["error"], Name = "throw",
            Property = global::PLang.Tests.Shared.Make.Properties(parameters),
            Modifier = modifiers ?? new List<global::app.goal.step.action.modifier.@this>()
        };
    }

    private static global::app.goal.step.action.modifier.@this ErrorHandler(params (string name, object? value)[] parameters)
    {
        var list = new List<global::app.data.@this>();
        foreach (var p in parameters) list.Add(new(p.name, p.value, context: global::PLang.Tests.TestApp.SharedContext));
        return new global::app.goal.step.action.modifier.@this
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["error"], Name = "handle",
            Property = global::PLang.Tests.Shared.Make.Properties(list)
        };
    }

    /// <summary>An error handler whose recovery chain calls <paramref name="goalName"/>.
    /// Recovery actions are structure on the modifier, not one of its parameters.</summary>
    private static global::app.goal.step.action.modifier.@this ErrorHandlerCalling(
        string goalName, params (string name, object? value)[] parameters)
    {
        var handler = ErrorHandler(parameters);
        handler.Recovery.Add(CallGoal(goalName));
        return handler;
    }

    /// <summary>One recovery action: a call to <paramref name="goalName"/>.</summary>
    private static PrAction CallGoal(string goalName) => new()
    {
        Module = global::PLang.Tests.TestApp.SharedContext.App.Module["goal"], Name = "call",
        Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
        {
            new("Name", goalName, context: global::PLang.Tests.TestApp.SharedContext)
        })
    };

    [Test]
    public async Task Handle_ActionSucceeds_PassesThrough()
    {
        var action = new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"], Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%ok%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "v", context: global::PLang.Tests.TestApp.SharedContext)
            }),
            Modifier = new List<global::app.goal.step.action.modifier.@this> { ErrorHandler(("ignoreError", true)) }
        };

        var result = await action.Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("ok"))).IsEqualTo("v");
    }

    [Test]
    public async Task Handle_IgnoreError_SwallowsErrorReturnsOk()
    {
        var action = Throw("boom",
            modifiers: new List<global::app.goal.step.action.modifier.@this> { ErrorHandler(("ignoreError", true)) });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }

    private global::app.channel.type.stream.@this CaptureDebug()
    {
        _app.System.Channel.Register(global::app.channel.type.stream.@this.Memory(global::app.channel.list.@this.Debug));
        return (global::app.channel.type.stream.@this)_app.System.Channel.Get(global::app.channel.list.@this.Debug)!;
    }

    private static string Read(global::app.channel.type.stream.@this capture)
    {
        capture.Stream.Position = 0;
        using var reader = new StreamReader(capture.Stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    [Test]
    public async Task Handle_IgnoreError_StaysInAudit_PrintedUnderDebug()
    {
        var capture = CaptureDebug();
        _app.Debug = new global::app.module.action.debug.@this(_app.System.Context);
        var action = Throw("boom", key: "Oops",
            modifiers: new List<global::app.goal.step.action.modifier.@this> { ErrorHandler(("ignoreError", true)) });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
        await Assert.That(Ctx.CallStack.Audit.Any(e => e.Message == "boom")).IsTrue();
        var written = Read(capture);
        await Assert.That(written).Contains("Oops");
        await Assert.That(written).Contains("boom");
    }

    [Test]
    public async Task Handle_IgnoreError_WithoutDebug_PrintsNothing()
    {
        var capture = CaptureDebug();
        var action = Throw("boom", key: "Oops",
            modifiers: new List<global::app.goal.step.action.modifier.@this> { ErrorHandler(("ignoreError", true)) });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
        await Assert.That(Ctx.CallStack.Audit.Any(e => e.Message == "boom")).IsTrue();
        await Assert.That(Read(capture)).IsEmpty();
    }

    [Test]
    public async Task Handle_FilterByStatusCode_MatchHandles()
    {
        var action = Throw("not found", statusCode: 404,
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("statusCode", 404), ("ignoreError", true))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_FilterByStatusCode_NoMatchPropagates()
    {
        var action = Throw("server error", statusCode: 500,
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("statusCode", 404), ("ignoreError", true))
            });

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Handle_FilterByKey_CaseInsensitiveMatch()
    {
        var action = Throw("broken", key: "NotFound",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("key", "notfound"), ("ignoreError", true))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_FilterByMessage_SubstringMatch()
    {
        var action = Throw("connection refused on port 443",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("message", "connection"), ("ignoreError", true))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_FilterByKey_Mismatch_PropagatesError()
    {
        var action = Throw("broken", key: "Timeout",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("key", "NotFound"), ("ignoreError", true))
            });

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task Handle_FilterByMessage_Mismatch_PropagatesError()
    {
        var action = Throw("disk full",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandler(("message", "connection"), ("ignoreError", true))
            });

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).IsEqualTo("disk full");
    }

    [Test]
    public async Task Handle_NoFilter_MatchesAllErrors()
    {
        var action = Throw("anything", statusCode: 418,
            modifiers: new List<global::app.goal.step.action.modifier.@this> { ErrorHandler(("ignoreError", true)) });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
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

        var modifiers = new List<global::app.goal.step.action.modifier.@this>
        {
            ErrorHandler(("retryCount", 2), ("order", "RetryFirst"))
        };

        await using var frame = TestFrame.Live(Ctx);
        var (wrapped, _) = await modifiers[0].Wrap(persistentlyFailing, Ctx);
        var result = await wrapped!();

        await result.IsFailure();
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

        var modifiers = new List<global::app.goal.step.action.modifier.@this>
        {
            ErrorHandler(("retryCount", 1), ("order", "GoalFirst"))
        };

        await using var frame = TestFrame.Live(Ctx);
        var (wrapped, _) = await modifiers[0].Wrap(persistentlyFailing, Ctx);
        var result = await wrapped!();

        await result.IsFailure();
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

        var modifiers = new List<global::app.goal.step.action.modifier.@this> { ErrorHandler(("retryCount", 3)) };

        await using var frame = TestFrame.Live(Ctx);
        var (wrapped, _) = await modifiers[0].Wrap(persistentlyFailing, Ctx);
        var result = await wrapped!();

        await result.IsFailure();
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

        var modifiers = new List<global::app.goal.step.action.modifier.@this>
        {
            ErrorHandler(("retryCount", 3))
        };

        await using var frame = TestFrame.Live(Ctx);
        var (wrapped, _) = await modifiers[0].Wrap(statefulNext, Ctx);
        var result = await wrapped!();

        await result.IsSuccess();
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
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module[module], Name = actionName,
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new global::app.data.@this(p.name, p.value,
                PrParam.IsVarNameSlot(module, actionName, p.name) ? new global::app.type.@this("variable") : null, context: global::PLang.Tests.TestApp.SharedContext)).ToList())
        };
        var step = new Step { Text = $"test step for {name}" };
        step.Action.Add(prAction);
        var goal = new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", global::PLang.Tests.TestApp.SharedContext) };
        goal.Step.Add(step);
        _app.Goal.Add(goal);
        return goal;
    }

    [Test]
    public async Task Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult()
    {
        RegisterGoal("SuccessGoal", "variable", "set", ("name", "%marker%"), ("value", "handled"));

        var action = Throw("boom",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandlerCalling("SuccessGoal", ("order", "GoalFirst"))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }

    // GoalFirst is fix, then retry: the goal runs first, then the step retries and gets the
    // retry's result — here the goal sets what the step needs, so the retry succeeds.
    [Test]
    public async Task Handle_GoalFirst_WithRetry_FixesThenRetries_StepGetsTheRetrysResult()
    {
        RegisterGoal("Fix", "variable", "set", ("name", "%fixed%"), ("value", "yes"));
        int callCount = 0;
        Func<Task<global::app.data.@this>> needsFix = async () =>
        {
            callCount++;
            var fixedFlag = await Ctx.Variable.Get("fixed");
            return fixedFlag.IsInitialized
                ? Ctx.Ok("done after fix")
                : global::app.data.@this.FromError(new global::app.error.ServiceError("not fixed yet", "NotFixed", 404));
        };

        await using var frame = TestFrame.Live(Ctx);
        var (wrapped, _) = await ErrorHandlerCalling("Fix", ("order", "GoalFirst"), ("retryCount", 1)).Wrap(needsFix, Ctx);
        var result = await wrapped!();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("done after fix");
        await Assert.That(callCount).IsEqualTo(2);   // the failing run, then the retry after the fix
    }

    // `on error key "FileNotFound" call A, on error call B` — the clauses of one step are one try/catch,
    // asked in the order written: the first whose filter matches handles, the other never sees it.
    private PrAction ThrowCaughtByAThenB(string key)
    {
        RegisterGoal("A", "variable", "set", ("name", "%ranA%"), ("value", "yes"));
        RegisterGoal("B", "variable", "set", ("name", "%ranB%"), ("value", "yes"));
        return Throw("boom", key: key, modifiers: new List<global::app.goal.step.action.modifier.@this>
        {
            ErrorHandlerCalling("A", ("key", "FileNotFound")),
            ErrorHandlerCalling("B")
        });
    }

    [Test]
    public async Task OnErrorClauses_KeyedError_GoesToTheFirstMatchOnly()
    {
        var result = await ThrowCaughtByAThenB("FileNotFound").Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.Get("ranA")).IsInitialized).IsTrue();
        await Assert.That((await Ctx.Variable.Get("ranB")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task OnErrorClauses_OtherError_SkipsTheKeyedClause_GoesToTheNext()
    {
        var result = await ThrowCaughtByAThenB("SomethingElse").Run(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.Get("ranA")).IsInitialized).IsFalse();
        await Assert.That((await Ctx.Variable.Get("ranB")).IsInitialized).IsTrue();
    }

    [Test]
    public async Task OnErrorClauses_ThrowFromTheMatchingHandler_EscapesTheOthers()
    {
        RegisterGoal("A", "error", "throw", ("message", "thrown by A"), ("key", "FromA"));
        RegisterGoal("B", "variable", "set", ("name", "%ranB%"), ("value", "yes"));
        var action = Throw("boom", key: "FileNotFound", modifiers: new List<global::app.goal.step.action.modifier.@this>
        {
            ErrorHandlerCalling("A", ("key", "FileNotFound")),
            ErrorHandlerCalling("B")
        });

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That((await Ctx.Variable.Get("ranB")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task Handle_GoalFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal", "error", "throw", ("message", "goal failed"));

        var action = Throw("original error",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandlerCalling("FailGoal", ("order", "GoalFirst"))
            });

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.list.Count).IsGreaterThan(0);
        await Assert.That(result.Error.list[0].Message).IsEqualTo("goal failed");
    }

    [Test]
    public async Task Handle_RetryFirst_GoalSucceeds_ReturnsOk()
    {
        RegisterGoal("SuccessGoal2", "variable", "set", ("name", "%marker2%"), ("value", "ok"));

        var action = Throw("persistent",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandlerCalling("SuccessGoal2", ("order", "RetryFirst"))
            });

        var result = await action.Run(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_RetryFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal2", "error", "throw", ("message", "goal also failed"));

        var action = Throw("persistent",
            modifiers: new List<global::app.goal.step.action.modifier.@this>
            {
                ErrorHandlerCalling("FailGoal2", ("order", "RetryFirst"))
            });

        var result = await action.Run(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.list.Count).IsGreaterThan(0);
        await Assert.That(result.Error.list[0].Message).IsEqualTo("goal also failed");
    }
}
