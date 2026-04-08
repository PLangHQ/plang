using global::App.modules.error;
using global::App.Goals.Goal.Steps.Step;
using global::App.Errors;

namespace PLang.Tests.App.Modules.error;

public class ErrorCheckTests
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

    private static Data MakeError(string message = "boom", string key = "TestError", int statusCode = 400)
        => Data.FromError(new Error(message, key, statusCode));

    private static Step MakeStep(ErrorHandler? onError = null)
        => new() { Text = "test step", Index = 0, OnError = onError };

    #region Propagation

    [Test]
    public async Task Check_NullData_ReturnsOk()
    {
        var action = new Check { Context = Ctx, Data = null!, Step = MakeStep() };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_SuccessData_PassesThrough()
    {
        var ok = Data.Ok("good");
        var action = new Check { Context = Ctx, Data = ok, Step = MakeStep() };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("good");
    }

    [Test]
    public async Task Check_ErrorNoHandler_Propagates()
    {
        var error = MakeError();
        var step = MakeStep(onError: null);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    [Test]
    public async Task Check_ErrorNullStep_Propagates()
    {
        var error = MakeError();
        var action = new Check { Context = Ctx, Data = error, Step = null };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    #endregion

    #region Filtering

    [Test]
    public async Task Check_FilterByMessage_MatchesSubstring()
    {
        var error = MakeError(message: "connection refused");
        var handler = new ErrorHandler { IgnoreError = true, Message = "refused" };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        // Matched + ignore → Ok
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_FilterByMessage_NoMatch_Propagates()
    {
        var error = MakeError(message: "connection refused");
        var handler = new ErrorHandler { IgnoreError = true, Message = "timeout" };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    [Test]
    public async Task Check_FilterByStatusCode_Matches()
    {
        var error = MakeError(statusCode: 404);
        var handler = new ErrorHandler { IgnoreError = true, StatusCode = 404 };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_FilterByStatusCode_NoMatch_Propagates()
    {
        var error = MakeError(statusCode: 500);
        var handler = new ErrorHandler { IgnoreError = true, StatusCode = 404 };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    [Test]
    public async Task Check_FilterByKey_Matches()
    {
        var error = MakeError(key: "HttpError");
        var handler = new ErrorHandler { IgnoreError = true, Key = "httperror" }; // case insensitive
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_FilterByKey_NoMatch_Propagates()
    {
        var error = MakeError(key: "HttpError");
        var handler = new ErrorHandler { IgnoreError = true, Key = "IOError" };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Check_MultipleFilters_AllMustMatch()
    {
        var error = MakeError(message: "not found", key: "HttpError", statusCode: 404);
        // Message matches but key doesn't
        var handler = new ErrorHandler { IgnoreError = true, Message = "not found", Key = "IOError" };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    #endregion

    #region Ignore

    [Test]
    public async Task Check_IgnoreError_ReturnsOk()
    {
        var error = MakeError();
        var handler = new ErrorHandler { IgnoreError = true };
        var action = new Check { Context = Ctx, Data = error, Step = MakeStep(handler) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Retry

    [Test]
    public async Task Check_RetryWithNoActions_Succeeds()
    {
        // Step has no actions → retry loop runs app.Run on empty collection → Ok
        var error = MakeError();
        var handler = new ErrorHandler { RetryCount = 2 };
        var step = MakeStep(handler);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_RetryCountNull_FallsThrough()
    {
        var error = MakeError();
        var handler = new ErrorHandler { RetryCount = null };
        var step = MakeStep(handler);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        // No retry, no goal, no ignore → propagate
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    [Test]
    public async Task Check_RetryCountZero_FallsThrough()
    {
        var error = MakeError();
        var handler = new ErrorHandler { RetryCount = 0 };
        var step = MakeStep(handler);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    #endregion

    #region Error Goal

    [Test]
    public async Task Check_ErrorGoal_RetryFirst_GoalRunsAsFallback()
    {
        // Register a stub error goal
        _app.Goals.Add(new global::App.Goals.Goal.@this
        {
            Name = "HandleError",
            Path = "/HandleError.goal"
        });

        var error = MakeError();
        var handler = new ErrorHandler
        {
            Order = ErrorOrder.RetryFirst,
            Goal = new GoalCall { Name = "HandleError" }
        };
        var step = MakeStep(handler);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        // No retry configured → fallback to goal → Ok
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_ErrorGoal_GoalFirst_GoalRunsFirst()
    {
        _app.Goals.Add(new global::App.Goals.Goal.@this
        {
            Name = "FixFirst",
            Path = "/FixFirst.goal"
        });

        var error = MakeError();
        var handler = new ErrorHandler
        {
            Order = ErrorOrder.GoalFirst,
            Goal = new GoalCall { Name = "FixFirst" }
        };
        var step = MakeStep(handler);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        // GoalFirst: goal runs → no retry → goal alone counts as handled → Ok
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Check_ErrorGoal_InjectsErrorParameter()
    {
        // Verify the error goal actually runs by checking !error was injected on Variables
        _app.Goals.Add(new global::App.Goals.Goal.@this
        {
            Name = "ErrorReceiver",
            Path = "/ErrorReceiver.goal"
        });

        var error = MakeError(message: "db timeout", key: "DatabaseError", statusCode: 503);
        var handler = new ErrorHandler
        {
            Order = ErrorOrder.RetryFirst,
            Goal = new GoalCall { Name = "ErrorReceiver" }
        };
        var step = MakeStep(handler);
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();

        // RunGoalAsync injects GoalCall.Parameters on context.Variables
        // CallErrorGoal adds !error parameter with the actual error
        var injectedError = Ctx.Variables.Get("!error");
        await Assert.That(injectedError).IsNotNull();
        // The error value should be the IError from the failing Data
        await Assert.That(injectedError!.Value).IsNotNull();
    }

    [Test]
    public async Task Check_RetryExhausted_NoGoal_Propagates()
    {
        // Step with a real action that always fails — retry exhausts, no goal fallback
        var error = MakeError();
        var handler = new ErrorHandler { RetryCount = 2 };
        var step = MakeStep(handler);
        // Add an action that the module system doesn't know — app.Run returns error
        step.Actions.Add(new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "nonexistent",
            ActionName = "fail"
        });
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        // Retry ran actions, all failed, no goal configured → propagate
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Handled).IsFalse();
    }

    [Test]
    public async Task Check_RetryExhausted_WithGoalFallback_Succeeds()
    {
        _app.Goals.Add(new global::App.Goals.Goal.@this
        {
            Name = "FallbackGoal",
            Path = "/FallbackGoal.goal"
        });

        var error = MakeError();
        var handler = new ErrorHandler
        {
            RetryCount = 1,
            Order = ErrorOrder.RetryFirst,
            Goal = new GoalCall { Name = "FallbackGoal" }
        };
        var step = MakeStep(handler);
        // Action that fails (module not found)
        step.Actions.Add(new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
        {
            Module = "nonexistent",
            ActionName = "fail"
        });
        var action = new Check { Context = Ctx, Data = error, Step = step };
        var result = await action.Run();

        // Retry exhausted → fallback to goal → Ok
        await Assert.That(result.Success).IsTrue();
    }

    #endregion
}
