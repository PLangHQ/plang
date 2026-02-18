using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Core;

public class EventIntegrationTests
{
    private Engine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new Engine("/test");
    }

    [Test]
    public async Task BeforeGoal_FiresWhenGoalExecutes()
    {
        bool fired = false;

        var goal = new Goal { Name = "TestGoal" };

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                fired = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        await _engine.RunGoalAsync(goal);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task AfterGoal_FiresAfterGoalCompletes()
    {
        bool fired = false;

        var goal = new Goal { Name = "TestGoal" };

        _engine.Context.User.Events.Register(
            EventType.AfterGoal,
            ctx =>
            {
                fired = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        await _engine.RunGoalAsync(goal);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task BeforeStep_FiresWhenStepExecutes()
    {
        bool fired = false;

        var step = new Step { Index = 0, Text = "test step" };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        _engine.Context.User.Events.Register(
            EventType.BeforeStep,
            ctx =>
            {
                fired = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal",
            stepPattern: "test step");

        await _engine.RunGoalAsync(goal);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task AfterStep_FiresAfterStepCompletes()
    {
        bool fired = false;

        var step = new Step { Index = 0, Text = "test step" };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        _engine.Context.User.Events.Register(
            EventType.AfterStep,
            ctx =>
            {
                fired = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal",
            stepPattern: "test step");

        await _engine.RunGoalAsync(goal);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task BeforeGoal_ReturningFailure_StopsGoalExecution()
    {
        bool stepExecuted = false;

        var step = new Step { Index = 0, Text = "should not run" };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        _engine.Context.User.Events.Register(
            EventType.BeforeStep,
            ctx =>
            {
                stepExecuted = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx => Task.FromResult(Data.FromError(new Error("blocked"))),
            goalNamePattern: "TestGoal");

        var result = await _engine.RunGoalAsync(goal);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(stepExecuted).IsFalse();
    }

    [Test]
    public async Task EventHandler_CanSetVariableVisibleToSteps()
    {
        string? capturedValue = null;

        var goal = new Goal { Name = "TestGoal" };

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                ctx.MemoryStack.Set("eventVar", "from-event");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        _engine.Context.User.Events.Register(
            EventType.AfterGoal,
            ctx =>
            {
                capturedValue = ctx.MemoryStack.Get<string>("eventVar");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        await _engine.RunGoalAsync(goal);

        await Assert.That(capturedValue).IsEqualTo("from-event");
    }

    [Test]
    public async Task PriorityOrdering_HigherPriorityRunsFirst()
    {
        var order = new List<string>();

        var goal = new Goal { Name = "TestGoal" };

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                order.Add("low");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal",
            priority: 1);

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                order.Add("high");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal",
            priority: 10);

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                order.Add("medium");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal",
            priority: 5);

        await _engine.RunGoalAsync(goal);

        await Assert.That(order.Count).IsEqualTo(3);
        await Assert.That(order[0]).IsEqualTo("high");
        await Assert.That(order[1]).IsEqualTo("medium");
        await Assert.That(order[2]).IsEqualTo("low");
    }

    [Test]
    public async Task EventExecution_Order_BeforeGoal_BeforeStep_AfterStep_AfterGoal()
    {
        var order = new List<string>();

        var step = new Step { Index = 0, Text = "test step" };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        _engine.Context.User.Events.Register(
            EventType.BeforeStep,
            ctx =>
            {
                order.Add("BeforeStep");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        _engine.Context.User.Events.Register(
            EventType.AfterStep,
            ctx =>
            {
                order.Add("AfterStep");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        _engine.Context.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                order.Add("BeforeGoal");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        _engine.Context.User.Events.Register(
            EventType.AfterGoal,
            ctx =>
            {
                order.Add("AfterGoal");
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "TestGoal");

        await _engine.RunGoalAsync(goal);

        await Assert.That(order.Count).IsEqualTo(4);
        await Assert.That(order[0]).IsEqualTo("BeforeGoal");
        await Assert.That(order[1]).IsEqualTo("BeforeStep");
        await Assert.That(order[2]).IsEqualTo("AfterStep");
        await Assert.That(order[3]).IsEqualTo("AfterGoal");
    }

    [Test]
    public async Task Events_Register_DispatchesCorrectly()
    {
        var events = new EngineEvents();
        bool fired = false;

        events.Register(EventType.BeforeGoal, ctx =>
        {
            fired = true;
            return Task.FromResult(Data.Ok());
        });

        var context = _engine.Context;
        var result = await events.DispatchAsync(context, EventType.BeforeGoal);

        await Assert.That(fired).IsTrue();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Events_GoalNamePattern_FiltersCorrectly()
    {
        var events = new EngineEvents();
        var results = new List<string>();

        events.Register(EventType.BeforeGoal, ctx =>
        {
            results.Add("all");
            return Task.FromResult(Data.Ok());
        }, goalNamePattern: "*");

        events.Register(EventType.BeforeGoal, ctx =>
        {
            results.Add("specific");
            return Task.FromResult(Data.Ok());
        }, goalNamePattern: "MyGoal");

        var context = _engine.Context;

        // Dispatch for MyGoal - both should fire
        await events.DispatchAsync(context, EventType.BeforeGoal, goalName: "MyGoal");
        await Assert.That(results.Count).IsEqualTo(2);

        results.Clear();

        // Dispatch for OtherGoal - only wildcard should fire
        await events.DispatchAsync(context, EventType.BeforeGoal, goalName: "OtherGoal");
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0]).IsEqualTo("all");
    }

    [Test]
    public async Task Events_Unregister_RemovesBinding()
    {
        var events = new EngineEvents();
        bool fired = false;

        var id = events.Register(EventType.BeforeGoal, ctx =>
        {
            fired = true;
            return Task.FromResult(Data.Ok());
        });

        events.Unregister(id);

        var context = _engine.Context;
        await events.DispatchAsync(context, EventType.BeforeGoal);

        await Assert.That(fired).IsFalse();
    }

    [Test]
    public async Task BeforeAction_FiresWhenActionExecutes()
    {
        bool fired = false;

        _engine.Context.User.Events.Register(
            EventType.BeforeAction,
            ctx =>
            {
                fired = true;
                return Task.FromResult(Data.Ok());
            },
            actionPattern: "variable.set");

        var step = new Step
        {
            Index = 0,
            Text = "set var",
            Actions = new StepActions(new[]
            {
                new PLang.Runtime2.Engine.Goals.Steps.Actions.Action
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data>
                    {
                        new Data("name", "x"),
                        new Data("value", "hello")
                    }
                }
            })
        };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        await _engine.RunGoalAsync(goal);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task AfterAction_FiresAfterActionExecutes()
    {
        bool fired = false;

        _engine.Context.User.Events.Register(
            EventType.AfterAction,
            ctx =>
            {
                fired = true;
                return Task.FromResult(Data.Ok());
            },
            actionPattern: "variable.set");

        var step = new Step
        {
            Index = 0,
            Text = "set var",
            Actions = new StepActions(new[]
            {
                new PLang.Runtime2.Engine.Goals.Steps.Actions.Action
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data>
                    {
                        new Data("name", "x"),
                        new Data("value", "hello")
                    }
                }
            })
        };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        await _engine.RunGoalAsync(goal);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task ActionPattern_Wildcard_MatchesAllActionsInModule()
    {
        int fireCount = 0;

        _engine.Context.User.Events.Register(
            EventType.BeforeAction,
            ctx =>
            {
                fireCount++;
                return Task.FromResult(Data.Ok());
            },
            actionPattern: "variable.*");

        var step = new Step
        {
            Index = 0,
            Text = "set var",
            Actions = new StepActions(new[]
            {
                new PLang.Runtime2.Engine.Goals.Steps.Actions.Action
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data>
                    {
                        new Data("name", "x"),
                        new Data("value", "hello")
                    }
                }
            })
        };

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new GoalSteps { step }
        };

        await _engine.RunGoalAsync(goal);

        await Assert.That(fireCount).IsEqualTo(1);
    }

    [Test]
    public async Task PerContextIsolation_TwoContexts_SameGoal_DifferentEvents()
    {
        var goal = new Goal { Name = "SharedGoal" };

        // Context 1 has an event
        var context1 = _engine.CreateContext();
        bool context1Fired = false;
        context1.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                context1Fired = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "SharedGoal");

        // Context 2 has no events
        var context2 = _engine.CreateContext();
        bool context2Fired = false;
        context2.User.Events.Register(
            EventType.BeforeGoal,
            ctx =>
            {
                context2Fired = true;
                return Task.FromResult(Data.Ok());
            },
            goalNamePattern: "OtherGoal"); // won't match

        await _engine.RunGoalAsync(goal, context1);
        await _engine.RunGoalAsync(goal, context2);

        await Assert.That(context1Fired).IsTrue();
        await Assert.That(context2Fired).IsFalse();
    }

    [Test]
    public async Task EventBinding_MatchesAction_ExactMatch()
    {
        var binding = new EventBinding(EventType.BeforeAction, _ => Task.FromResult(Data.Ok()), actionPattern: "variable.set");

        await Assert.That(binding.MatchesAction("variable", "set")).IsTrue();
        await Assert.That(binding.MatchesAction("variable", "get")).IsFalse();
        await Assert.That(binding.MatchesAction("file", "set")).IsFalse();
    }

    [Test]
    public async Task EventBinding_MatchesAction_WildcardModule()
    {
        var binding = new EventBinding(EventType.BeforeAction, _ => Task.FromResult(Data.Ok()), actionPattern: "variable.*");

        await Assert.That(binding.MatchesAction("variable", "set")).IsTrue();
        await Assert.That(binding.MatchesAction("variable", "get")).IsTrue();
        await Assert.That(binding.MatchesAction("file", "read")).IsFalse();
    }

    [Test]
    public async Task EventBinding_MatchesAction_NullPattern_MatchesAll()
    {
        var binding = new EventBinding(EventType.BeforeAction, _ => Task.FromResult(Data.Ok()));

        await Assert.That(binding.MatchesAction("variable", "set")).IsTrue();
        await Assert.That(binding.MatchesAction("file", "read")).IsTrue();
    }

    [Test]
    public async Task EventBinding_MatchesGoal_Regex()
    {
        var binding = new EventBinding(
            EventType.BeforeGoal,
            _ => Task.FromResult(Data.Ok()),
            goalNamePattern: "^Admin",
            isRegex: true);

        await Assert.That(binding.MatchesGoal("AdminGoal")).IsTrue();
        await Assert.That(binding.MatchesGoal("AdminUsers")).IsTrue();
        await Assert.That(binding.MatchesGoal("UserAdmin")).IsFalse();
    }

    [Test]
    public async Task EventBinding_MatchesStep_Regex()
    {
        var binding = new EventBinding(
            EventType.BeforeStep,
            _ => Task.FromResult(Data.Ok()),
            stepPattern: @"set\s+%\w+%",
            isRegex: true);

        await Assert.That(binding.MatchesStep("set %name%")).IsTrue();
        await Assert.That(binding.MatchesStep("set %count%")).IsTrue();
        await Assert.That(binding.MatchesStep("get something")).IsFalse();
    }

    [Test]
    public async Task EventBinding_MatchesAction_Regex()
    {
        var binding = new EventBinding(
            EventType.BeforeAction,
            _ => Task.FromResult(Data.Ok()),
            actionPattern: @"variable\.(set|get)",
            isRegex: true);

        await Assert.That(binding.MatchesAction("variable", "set")).IsTrue();
        await Assert.That(binding.MatchesAction("variable", "get")).IsTrue();
        await Assert.That(binding.MatchesAction("variable", "remove")).IsFalse();
        await Assert.That(binding.MatchesAction("file", "read")).IsFalse();
    }

    [Test]
    public async Task EventBinding_Regex_IsCaseInsensitive()
    {
        var binding = new EventBinding(
            EventType.BeforeGoal,
            _ => Task.FromResult(Data.Ok()),
            goalNamePattern: "^admin",
            isRegex: true);

        await Assert.That(binding.MatchesGoal("AdminGoal")).IsTrue();
        await Assert.That(binding.MatchesGoal("ADMINGOAL")).IsTrue();
        await Assert.That(binding.MatchesGoal("admingoal")).IsTrue();
    }
}
