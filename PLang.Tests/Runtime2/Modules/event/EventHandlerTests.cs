using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules.@event;
using EventResult = PLang.Runtime2.modules.@event.types.@event;

namespace PLang.Tests.Runtime2.modules.EventTests;

public class EventHandlerTests
{
    private Engine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new Engine("/test");
    }

    private PLangContext CreateContext()
    {
        return _engine.CreateContext();
    }

    [Test]
    public async Task BeforeGoal_RegistersEvent()
    {
        var context = CreateContext();

        var handler = new BeforeGoal
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogGoal" },
            GoalPattern = "TestGoal",
            IsRegex = false,
            Priority = 0
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev).IsNotNull();
        await Assert.That(ev!.type).IsEqualTo("beforeGoal");
        await Assert.That(ev.goalToCall).IsEqualTo("LogGoal");
        await Assert.That(ev.pattern).IsEqualTo("TestGoal");
        await Assert.That(context.User.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AfterGoal_RegistersEvent()
    {
        var context = CreateContext();

        var handler = new AfterGoal
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogGoal" },
            GoalPattern = "*",
            IsRegex = false,
            Priority = 0
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev).IsNotNull();
        await Assert.That(ev!.type).IsEqualTo("afterGoal");
        await Assert.That(context.User.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BeforeStep_RegistersEvent()
    {
        var context = CreateContext();

        var handler = new BeforeStep
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogStep" },
            GoalPattern = "TestGoal",
            StepPattern = "set*",
            IsRegex = false,
            Priority = 0
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev).IsNotNull();
        await Assert.That(ev!.type).IsEqualTo("beforeStep");
        await Assert.That(context.User.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AfterStep_RegistersEvent()
    {
        var context = CreateContext();

        var handler = new AfterStep
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogStep" },
            GoalPattern = null,
            StepPattern = null,
            IsRegex = false,
            Priority = 5
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev).IsNotNull();
        await Assert.That(ev!.type).IsEqualTo("afterStep");
        await Assert.That(context.User.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BeforeAction_RegistersEvent()
    {
        var context = CreateContext();

        var handler = new BeforeAction
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "OnVarSet" },
            ActionPattern = "variable.set",
            IsRegex = false,
            Priority = 0
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev).IsNotNull();
        await Assert.That(ev!.type).IsEqualTo("beforeAction");
        await Assert.That(ev.pattern).IsEqualTo("variable.set");
        await Assert.That(context.User.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AfterAction_RegistersEvent()
    {
        var context = CreateContext();

        var handler = new AfterAction
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "OnAfterAction" },
            ActionPattern = "variable.*",
            IsRegex = false,
            Priority = 0
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev).IsNotNull();
        await Assert.That(ev!.type).IsEqualTo("afterAction");
        await Assert.That(context.User.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Remove_UnregistersEvent()
    {
        var context = CreateContext();

        // First register an event
        var beforeHandler = new BeforeGoal
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogGoal" },
            GoalPattern = "*",
            IsRegex = false,
            Priority = 0
        };
        var registerResult = await beforeHandler.Run();
        var ev = registerResult.Value as EventResult;

        await Assert.That(context.User.Events.Count).IsEqualTo(1);

        // Now remove it
        var removeHandler = new PLang.Runtime2.modules.@event.Remove
        {
            Context = context,
            EventId = ev!.id
        };
        var removeResult = await removeHandler.Run();

        await Assert.That(removeResult.Success).IsTrue();
        await Assert.That(context.User.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BeforeGoal_WithRegex_RegistersWithIsRegexTrue()
    {
        var context = CreateContext();

        var handler = new BeforeGoal
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogGoal" },
            GoalPattern = "^Admin",
            IsRegex = true,
            Priority = 0
        };

        var result = await handler.Run();

        await Assert.That(result.Success).IsTrue();
        var ev = result.Value as EventResult;
        await Assert.That(ev!.isRegex).IsTrue();

        // Verify the binding matches regex patterns
        var bindings = context.User.Events.GetMatchingBindings(
            EventType.BeforeGoal, goalName: "AdminGoal");
        await Assert.That(bindings.Count).IsEqualTo(1);

        var noMatch = context.User.Events.GetMatchingBindings(
            EventType.BeforeGoal, goalName: "UserGoal");
        await Assert.That(noMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalPattern_Wildcard_MatchesPrefix()
    {
        var context = CreateContext();

        var handler = new BeforeGoal
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "LogGoal" },
            GoalPattern = "/admin/*",
            IsRegex = false,
            Priority = 0
        };

        await handler.Run();

        var match = context.User.Events.GetMatchingBindings(
            EventType.BeforeGoal, goalName: "/admin/Users");
        await Assert.That(match.Count).IsEqualTo(1);

        var noMatch = context.User.Events.GetMatchingBindings(
            EventType.BeforeGoal, goalName: "/public/Home");
        await Assert.That(noMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ActionPattern_Wildcard_MatchesModule()
    {
        var context = CreateContext();

        var handler = new BeforeAction
        {
            Context = context,
            GoalToCall = new GoalCall { Name = "OnVar" },
            ActionPattern = "variable.*",
            IsRegex = false,
            Priority = 0
        };

        await handler.Run();

        var match = context.User.Events.GetMatchingBindings(
            EventType.BeforeAction, module: "variable", actionName: "set");
        await Assert.That(match.Count).IsEqualTo(1);

        var match2 = context.User.Events.GetMatchingBindings(
            EventType.BeforeAction, module: "variable", actionName: "get");
        await Assert.That(match2.Count).IsEqualTo(1);

        var noMatch = context.User.Events.GetMatchingBindings(
            EventType.BeforeAction, module: "file", actionName: "read");
        await Assert.That(noMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PerContextIsolation_TwoContexts_DifferentEvents()
    {
        var context1 = CreateContext();
        var context2 = CreateContext();

        var handler1 = new BeforeGoal
        {
            Context = context1,
            GoalToCall = new GoalCall { Name = "LogGoal" },
            GoalPattern = "TestGoal",
            IsRegex = false,
            Priority = 0
        };
        await handler1.Run();

        // context1 has an event, context2 does not
        await Assert.That(context1.User.Events.Count).IsEqualTo(1);
        await Assert.That(context2.User.Events.Count).IsEqualTo(0);
    }
}
