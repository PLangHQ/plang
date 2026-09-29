namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the on.error modifier handler.
/// Wraps an action with error matching, retry logic, and error goal calls.
/// </summary>
public class ErrorHandleTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // An error.throw with its on.error clauses after it (bound on it, as a program's read binds them).
    private PrAction Throw(string message, int? statusCode = null, string? key = null,
        PrAction[]? modifiers = null)
    {
        var parameters = new List<global::app.data.@this> { new("message", message, context: _app.actor.list.User.Context) };
        if (statusCode != null) parameters.Add(new("statusCode", statusCode.Value, context: _app.actor.list.User.Context));
        if (key != null) parameters.Add(new("key", key, context: _app.actor.list.User.Context));
        return global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = _app.actor.list.User.Context.App.Module("error"), Name = "throw",
            Property = global::PLang.Tests.Shared.Make.Properties(parameters),
        }, modifiers ?? []);
    }

    // An on.error clause with these properties.
    private PrAction ErrorHandler(params (string name, object? value)[] parameters)
        => global::PLang.Tests.Shared.Make.Action(Ctx, "on", "error", parameters);

    /// <summary>An on.error clause whose Recovery calls <paramref name="goalName"/>.</summary>
    private PrAction ErrorHandlerCalling(string goalName, params (string name, object? value)[] parameters)
        => global::PLang.Tests.Shared.Make.Action(Ctx, "on", "error",
            [.. parameters, global::PLang.Tests.Shared.Make.Recovery(Ctx, CallGoal(goalName))]);

    /// <summary>One recovery action: a call to <paramref name="goalName"/>.</summary>
    private PrAction CallGoal(string goalName) => new()
    {
        Module = _app.actor.list.User.Context.App.Module("goal"), Name = "call",
        Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
        {
            new("Name", goalName, context: _app.actor.list.User.Context)
        })
    };

    [Test]
    public async Task Handle_ActionSucceeds_PassesThrough()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%ok%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "v", context: _app.actor.list.User.Context)
            })
        }, ErrorHandler(("ignoreError", true)));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("ok"))).IsEqualTo("v");
    }

    [Test]
    public async Task Handle_IgnoreError_SwallowsErrorReturnsOk()
    {
        var action = Throw("boom",
            modifiers: new PrAction[] { ErrorHandler(("ignoreError", true)) });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    private global::app.channel.type.stream.@this CaptureDebug()
    {
        _app.actor.list.System.Channel.Register(global::app.channel.type.stream.@this.Memory(global::app.channel.list.@this.Debug));
        return (global::app.channel.type.stream.@this)_app.actor.list.System.Channel.Get(global::app.channel.list.@this.Debug)!;
    }

    private string Read(global::app.channel.type.stream.@this capture)
    {
        capture.Stream.Position = 0;
        using var reader = new StreamReader(capture.Stream, leaveOpen: true);
        return reader.ReadToEnd();
    }

    [Test]
    public async Task Handle_IgnoreError_StaysInAudit_PrintedUnderDebug()
    {
        var capture = CaptureDebug();
        _app.Debug = new global::app.module.debug.@this(_app.actor.list.System.Context);
        var action = Throw("boom", key: "Oops",
            modifiers: new PrAction[] { ErrorHandler(("ignoreError", true)) });

        var result = await action.Start(Ctx);

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
            modifiers: new PrAction[] { ErrorHandler(("ignoreError", true)) });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That(Ctx.CallStack.Audit.Any(e => e.Message == "boom")).IsTrue();
        await Assert.That(Read(capture)).IsEmpty();
    }

    [Test]
    public async Task Handle_FilterByStatusCode_MatchHandles()
    {
        var action = Throw("not found", statusCode: 404,
            modifiers: new PrAction[]
            {
                ErrorHandler(("statusCode", 404), ("ignoreError", true))
            });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_FilterByStatusCode_NoMatchPropagates()
    {
        var action = Throw("server error", statusCode: 500,
            modifiers: new PrAction[]
            {
                ErrorHandler(("statusCode", 404), ("ignoreError", true))
            });

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Handle_FilterByKey_CaseInsensitiveMatch()
    {
        var action = Throw("broken", key: "NotFound",
            modifiers: new PrAction[]
            {
                ErrorHandler(("key", "notfound"), ("ignoreError", true))
            });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_FilterByMessage_SubstringMatch()
    {
        var action = Throw("connection refused on port 443",
            modifiers: new PrAction[]
            {
                ErrorHandler(("message", "connection"), ("ignoreError", true))
            });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_FilterByKey_Mismatch_PropagatesError()
    {
        var action = Throw("broken", key: "Timeout",
            modifiers: new PrAction[]
            {
                ErrorHandler(("key", "NotFound"), ("ignoreError", true))
            });

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task Handle_FilterByMessage_Mismatch_PropagatesError()
    {
        var action = Throw("disk full",
            modifiers: new PrAction[]
            {
                ErrorHandler(("message", "connection"), ("ignoreError", true))
            });

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).IsEqualTo("disk full");
    }

    [Test]
    public async Task Handle_NoFilter_MatchesAllErrors()
    {
        var action = Throw("anything", statusCode: 418,
            modifiers: new PrAction[] { ErrorHandler(("ignoreError", true)) });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    // An action whose every attempt is `attempt`: a binding before its start answers in the dispatch's place
    // (a success cancels the dispatch; a failure is the attempt's) — each retry is another attempt.
    private PrAction Attempting(Func<Task<global::app.data.@this>> attempt, params PrAction[] clauses)
    {
        var action = new PrAction
        {
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>()),
        };
        action.Own().Bind("start", global::app.@event.When.before, async (_, _, _) =>
        {
            var answer = await attempt();
            if (answer.Success) answer.Handled = true;
            return answer;
        }, _app.actor.list.User, global::app.@event.binding.Scope.actor);
        return global::PLang.Tests.Shared.Make.With(action, clauses);
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

        var result = await Attempting(persistentlyFailing, ErrorHandler(("retryCount", 2), ("order", "RetryFirst"))).Start(Ctx);

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

        var result = await Attempting(persistentlyFailing, ErrorHandler(("retryCount", 1), ("order", "GoalFirst"))).Start(Ctx);

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

        var result = await Attempting(persistentlyFailing, ErrorHandler(("retryCount", 3))).Start(Ctx);

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

        var result = await Attempting(statefulNext, ErrorHandler(("retryCount", 3))).Start(Ctx);

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
            Module = _app.actor.list.User.Context.App.Module(module), Name = actionName,
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new global::app.data.@this(p.name, p.value,
                PrParam.IsVarNameSlot(module, actionName, p.name) ? new global::app.type.@this("variable") : null, context: _app.actor.list.User.Context)).ToList())
        };
        var step = new Step { Text = $"test step for {name}" };
        step.Code.Add(prAction);
        var goal = new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", _app.actor.list.User.Context) };
        goal.Step.Add(step);
        _app.goal.list.Add(goal);
        return goal;
    }

    [Test]
    public async Task Handle_GoalFirst_GoalSucceeds_ReturnsGoalResult()
    {
        RegisterGoal("SuccessGoal", "variable", "set", ("name", "%marker%"), ("value", "handled"));

        var action = Throw("boom",
            modifiers: new PrAction[]
            {
                ErrorHandlerCalling("SuccessGoal", ("order", "GoalFirst"))
            });

        var result = await action.Start(Ctx);

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

        var result = await Attempting(needsFix, ErrorHandlerCalling("Fix", ("order", "GoalFirst"), ("retryCount", 1))).Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("done after fix");
        await Assert.That(callCount).IsEqualTo(2);   // the failing run, then the retry after the fix
    }

    // `on error key "FileNotFound" call A, on error call B` — the clauses of one step answer the error outcome
    // in the order written: the first whose filter matches handles, the other never sees it.
    private PrAction ThrowCaughtByAThenB(string key)
    {
        RegisterGoal("A", "variable", "set", ("name", "%ranA%"), ("value", "yes"));
        RegisterGoal("B", "variable", "set", ("name", "%ranB%"), ("value", "yes"));
        return Throw("boom", key: key, modifiers: new PrAction[]
        {
            ErrorHandlerCalling("A", ("key", "FileNotFound")),
            ErrorHandlerCalling("B")
        });
    }

    [Test]
    public async Task OnErrorClauses_KeyedError_GoesToTheFirstMatchOnly()
    {
        var result = await ThrowCaughtByAThenB("FileNotFound").Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.Get("ranA")).IsInitialized).IsTrue();
        await Assert.That((await Ctx.Variable.Get("ranB")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task OnErrorClauses_OtherError_SkipsTheKeyedClause_GoesToTheNext()
    {
        var result = await ThrowCaughtByAThenB("SomethingElse").Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.Get("ranA")).IsInitialized).IsFalse();
        await Assert.That((await Ctx.Variable.Get("ranB")).IsInitialized).IsTrue();
    }

    [Test]
    public async Task OnErrorClauses_ThrowFromTheMatchingHandler_EscapesTheOthers()
    {
        RegisterGoal("A", "error", "throw", ("message", "thrown by A"), ("key", "FromA"));
        RegisterGoal("B", "variable", "set", ("name", "%ranB%"), ("value", "yes"));
        var action = Throw("boom", key: "FileNotFound", modifiers: new PrAction[]
        {
            ErrorHandlerCalling("A", ("key", "FileNotFound")),
            ErrorHandlerCalling("B")
        });

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That((await Ctx.Variable.Get("ranB")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task Handle_GoalFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal", "error", "throw", ("message", "goal failed"));

        var action = Throw("original error",
            modifiers: new PrAction[]
            {
                ErrorHandlerCalling("FailGoal", ("order", "GoalFirst"))
            });

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.list.Count).IsGreaterThan(0);
        await Assert.That(result.Error.list[0].Message).IsEqualTo("goal failed");
    }

    [Test]
    public async Task Handle_RetryFirst_GoalSucceeds_ReturnsOk()
    {
        RegisterGoal("SuccessGoal2", "variable", "set", ("name", "%marker2%"), ("value", "ok"));

        var action = Throw("persistent",
            modifiers: new PrAction[]
            {
                ErrorHandlerCalling("SuccessGoal2", ("order", "RetryFirst"))
            });

        var result = await action.Start(Ctx);

        await result.IsSuccess();
    }

    [Test]
    public async Task Handle_RetryFirst_GoalFails_ErrorChains()
    {
        RegisterGoal("FailGoal2", "error", "throw", ("message", "goal also failed"));

        var action = Throw("persistent",
            modifiers: new PrAction[]
            {
                ErrorHandlerCalling("FailGoal2", ("order", "RetryFirst"))
            });

        var result = await action.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.list.Count).IsGreaterThan(0);
        await Assert.That(result.Error.list[0].Message).IsEqualTo("goal also failed");
    }
}
