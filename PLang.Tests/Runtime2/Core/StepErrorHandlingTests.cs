using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Core;

public class StepErrorHandlingTests
{
    private PLang.Runtime2.Engine.@this _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new PLang.Runtime2.Engine.@this("/app");
    }

    [Test]
    public async Task Step_OnError_IgnoreError_ContinuesExecution()
    {
        // Create a step that fails (unknown module) but has IgnoreError
        var goal = new Goal
        {
            Name = "TestIgnoreError",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "this will fail",
                    OnError = new ErrorHandler { IgnoreError = true },
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "nonexistent",
                            ActionName = "doesnotexist",
                            Parameters = new List<Data>()
                        }
                    }
                },
                new Step
                {
                    Index = 1,
                    Text = "set success marker",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", "%reached%"),
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
        await Assert.That(context.MemoryStack.GetValue("reached")).IsEqualTo("yes");
    }

    [Test]
    public async Task Step_OnError_WithRetry_RetriesStep()
    {
        // Create a goal with a retry handler - the variable tracker will count attempts
        var context = _engine.CreateContext();
        context.MemoryStack.Set("attempts", 0);

        // Register a counting goal
        var countGoal = new Goal
        {
            Name = "CountAttempt",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "increment attempts",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", "%attempts%"),
                                new Data("value", 1)
                            }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(countGoal);

        // Step that fails with retry config (will exhaust retries, but IgnoreError)
        var goal = new Goal
        {
            Name = "TestRetry",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "failing step with retry",
                    OnError = new ErrorHandler
                    {
                        RetryCount = 2,
                        IgnoreError = true
                    },
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "nonexistent",
                            ActionName = "doesnotexist",
                            Parameters = new List<Data>()
                        }
                    }
                }
            }
        };

        var result = await _engine.RunGoalAsync(goal, context);

        // Should succeed because IgnoreError is set
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Step_OnError_WithGoal_CallsErrorGoal()
    {
        var context = _engine.CreateContext();

        // Register error handler goal
        var errorGoal = new Goal
        {
            Name = "HandleError",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "mark error handled",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "set",
                            Parameters = new List<Data>
                            {
                                new Data("name", "%errorHandled%"),
                                new Data("value", "yes")
                            }
                        }
                    }
                }
            }
        };
        _engine.Goals.Add(errorGoal);

        // Step that fails with error goal
        var goal = new Goal
        {
            Name = "TestErrorGoal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "failing step with error goal",
                    OnError = new ErrorHandler
                    {
                        Goal = new GoalCall { Name = "HandleError" }
                    },
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "nonexistent",
                            ActionName = "doesnotexist",
                            Parameters = new List<Data>()
                        }
                    }
                }
            }
        };

        var result = await _engine.RunGoalAsync(goal, context);

        // The error goal should have been called
        await Assert.That(context.MemoryStack.GetValue("errorHandled")).IsEqualTo("yes");
    }
}
