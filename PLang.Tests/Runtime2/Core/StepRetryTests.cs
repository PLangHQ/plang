using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Tests.Runtime2.Core;

/// <summary>
/// Tests for Step retry logic in StepMethods.RetryAsync and HandleErrorAsync.
/// Uses a FlakyHandler that can be configured to fail N times then succeed.
/// </summary>
public class StepRetryTests
{
    private PLang.Runtime2.Engine.@this _engine = null!;
    private FlakyHandler _flaky = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new PLang.Runtime2.Engine.@this("/app");
        _flaky = new FlakyHandler();
        _engine.Libraries.Register("test", "flaky", _flaky);
    }

    // ================================================================
    // Retry succeeds on Nth attempt
    // ================================================================

    [Test]
    public async Task Retry_SucceedsOnSecondAttempt()
    {
        _flaky.FailCount = 1; // fail once, succeed on 2nd call

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 3
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        // 1 initial call + 1 retry = handler called at least 2 times
        // (initial call fails in RunAsync, then RetryAsync calls again)
        await Assert.That(_flaky.CallCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Retry_SucceedsOnThirdAttempt()
    {
        _flaky.FailCount = 2; // fail twice, succeed on 3rd call

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 3
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_flaky.CallCount).IsGreaterThanOrEqualTo(3);
    }

    // ================================================================
    // Retry exhaustion
    // ================================================================

    [Test]
    public async Task Retry_ExhaustsAllRetries_ReturnsError()
    {
        _flaky.FailCount = 100; // always fails

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 3
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        // HandleErrorAsync returns the original error after retry+goal both fail
        await Assert.That(result.Error!.Message).Contains("Flaky fail");
        // Verify retries were actually attempted: 1 initial + 3 retries = 4 calls
        await Assert.That(_flaky.CallCount).IsEqualTo(4);
    }

    [Test]
    public async Task Retry_ExhaustsButIgnoreError_ReturnsOk()
    {
        _flaky.FailCount = 100; // always fails

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 2,
            IgnoreError = true
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        // Retries exhausted, but IgnoreError means overall success
        await Assert.That(result.Success).IsTrue();
    }

    // ================================================================
    // Retry count edge cases
    // ================================================================

    [Test]
    public async Task Retry_ZeroCount_SkipsRetry()
    {
        _flaky.FailCount = 100;

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 0,
            IgnoreError = true
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        // IgnoreError should still kick in, but no retries should happen
        await Assert.That(result.Success).IsTrue();
        await Assert.That(_flaky.CallCount).IsEqualTo(1); // only the initial call
    }

    [Test]
    public async Task Retry_NullCount_SkipsRetry()
    {
        _flaky.FailCount = 100;

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = null,
            IgnoreError = true
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_flaky.CallCount).IsEqualTo(1);
    }

    // ================================================================
    // Order: RetryFirst vs GoalFirst
    // ================================================================

    [Test]
    public async Task RetryFirst_RetriesBeforeCallingGoal()
    {
        _flaky.FailCount = 1; // fail once, succeed on retry

        var errorGoal = CreateVariableSetGoal("HandleError", "errorGoalCalled", "yes");
        _engine.Goals.Add(errorGoal);

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 3,
            Order = ErrorOrder.RetryFirst,
            Goal = new GoalCall { Name = "HandleError" }
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        // Retry succeeded, so error goal should NOT have been called
        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("errorGoalCalled")).IsNull();
    }

    [Test]
    public async Task GoalFirst_CallsGoalBeforeRetry()
    {
        _flaky.FailCount = 100; // always fails

        var errorGoal = CreateVariableSetGoal("HandleError", "goalOrder", "called");
        _engine.Goals.Add(errorGoal);

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 2,
            Order = ErrorOrder.GoalFirst,
            Goal = new GoalCall { Name = "HandleError" },
            IgnoreError = true
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        // Error goal was called (GoalFirst order)
        await Assert.That(context.MemoryStack.GetValue("goalOrder")).IsEqualTo("called");
        // IgnoreError ensures overall success
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task RetryFirst_FailsThenCallsGoal()
    {
        _flaky.FailCount = 100; // always fails

        var errorGoal = CreateVariableSetGoal("HandleError", "goalCalled", "yes");
        _engine.Goals.Add(errorGoal);

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 2,
            Order = ErrorOrder.RetryFirst,
            Goal = new GoalCall { Name = "HandleError" }
        });

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        // Retries exhausted, then error goal was called
        await Assert.That(context.MemoryStack.GetValue("goalCalled")).IsEqualTo("yes");
    }

    // ================================================================
    // Error goal receives error info
    // ================================================================

    [Test]
    public async Task ErrorGoal_ReceivesErrorVariables()
    {
        _flaky.FailCount = 100;

        // Error goal captures the error info into named variables
        var errorGoal = new Goal
        {
            Name = "CaptureError",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "capture error message",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", "%capturedError%"),
                                new Data("value", "%__error__%")
                            }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(errorGoal);

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            Goal = new GoalCall { Name = "CaptureError" },
            IgnoreError = true
        });

        var context = _engine.CreateContext();
        await _engine.RunGoalAsync(goal, context);

        // The error goal should have captured the error message
        var captured = context.MemoryStack.GetValue("capturedError");
        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.ToString()).Contains("Flaky fail");
    }

    [Test]
    public async Task ErrorGoal_CleansUpErrorVariables()
    {
        _flaky.FailCount = 100;

        var errorGoal = CreateVariableSetGoal("HandleError", "handled", "yes");
        _engine.Goals.Add(errorGoal);

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            Goal = new GoalCall { Name = "HandleError" },
            IgnoreError = true
        });

        var context = _engine.CreateContext();
        await _engine.RunGoalAsync(goal, context);

        // __error__ variables should be cleaned up after error goal runs
        await Assert.That(context.MemoryStack.GetValue("__error__")).IsNull();
        await Assert.That(context.MemoryStack.GetValue("__errorKey__")).IsNull();
        await Assert.That(context.MemoryStack.GetValue("__errorStatusCode__")).IsNull();
    }

    // ================================================================
    // Cancellation during retry
    // ================================================================

    [Test]
    public async Task Retry_Cancellation_StopsRetrying()
    {
        _flaky.FailCount = 100; // always fails

        var cts = new CancellationTokenSource();

        var goal = CreateGoalWithFlakyStep(new ErrorHandler
        {
            RetryCount = 50,
            RetryOverSeconds = 10 // ~200ms between retries
        });

        var context = _engine.CreateContext();

        // Cancel after a short delay
        cts.CancelAfter(50);

        var result = await _engine.RunGoalAsync(goal, context, cts.Token);

        // Should fail with cancellation error (graceful, not exception)
        await Assert.That(result.Success).IsFalse();
        // Should not have run all 50 retries
        await Assert.That(_flaky.CallCount).IsLessThan(50);
    }

    // ================================================================
    // Execution continues after successful error handling
    // ================================================================

    [Test]
    public async Task Retry_Success_ExecutionContinuesToNextStep()
    {
        _flaky.FailCount = 1; // fail once, succeed on retry

        var goal = new Goal
        {
            Name = "TestContinuation",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "flaky step with retry",
                    OnError = new ErrorHandler { RetryCount = 3 },
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "test",
                            ActionName = "flaky",
                            Parameters = new List<Data>()
                        }
                    }
                },
                new Step
                {
                    Index = 1,
                    Text = "set marker after retry success",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", "%continued%"),
                                new Data("value", "yes")
                            }
                        }
                    }
                }
            }
        };

        var context = _engine.CreateContext();
        var result = await _engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("continued")).IsEqualTo("yes");
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static Goal CreateGoalWithFlakyStep(ErrorHandler errorHandler)
    {
        return new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "flaky step",
                    OnError = errorHandler,
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "test",
                            ActionName = "flaky",
                            Parameters = new List<Data>()
                        }
                    }
                }
            }
        };
    }

    private static Goal CreateVariableSetGoal(string goalName, string varName, string varValue)
    {
        return new Goal
        {
            Name = goalName,
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = $"set {varName}",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", $"%{varName}%"),
                                new Data("value", varValue)
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// A controllable test handler that fails a configurable number of times
    /// before succeeding. Implements both IAction and ICodeGenerated.
    /// </summary>
    private sealed class FlakyHandler : IAction, ICodeGenerated
    {
        public int FailCount { get; set; } = 0;
        public int CallCount { get; private set; } = 0;

        public PLang.Runtime2.Engine.@this Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(PLang.Runtime2.Engine.@this engine, PLangContext context)
        {
            Engine = engine;
            Context = context;
        }

        public Task<Data> ExecuteAsync(object? parameters)
        {
            CallCount++;
            if (CallCount <= FailCount)
                return Task.FromResult(Data.FromError(
                    new PLang.Runtime2.Engine.Errors.Error($"Flaky fail #{CallCount}", "FlakyError", 500)));

            return Task.FromResult(Data.Ok("success"));
        }

        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, PLang.Runtime2.Engine.@this engine, PLangContext context)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
    }
}
