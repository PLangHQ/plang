using global::App.Actor.Context;
using App;
using global::App.Variables;
using global::App.Events;
using global::App.modules.@event;

namespace PLang.Tests.App.actions.EventTests;

public class EventHandlerTests
{
    private global::App.@this _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new global::App.@this("/test");
    }

    private global::App.Actor.Context.@this CreateContext() => _engine.CreateContext();

    private On MakeOn(global::App.Actor.Context.@this context, string type, string goalName,
        string? goalPattern = null, string? stepPattern = null, string? actionPattern = null,
        bool isRegex = false, int priority = 0)
        => new()
        {
            Context = context,
            Type = type,
            GoalToCall = new GoalCall { Name = goalName },
            GoalPattern = goalPattern,
            StepPattern = stepPattern,
            ActionPattern = actionPattern,
            IsRegex = isRegex,
            Priority = priority
        };

    [Test]
    public async Task On_BeforeGoal_RegistersEvent()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "BeforeGoal", "LogGoal", goalPattern: "TestGoal").Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value is string).IsTrue(); // returns binding id
        await Assert.That(context.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task On_AfterGoal_RegistersEvent()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "AfterGoal", "LogGoal", goalPattern: "*").Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task On_BeforeStep_RegistersEvent()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "BeforeStep", "LogStep", goalPattern: "TestGoal", stepPattern: "set*").Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task On_AfterStep_RegistersEvent()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "AfterStep", "LogStep", priority: 5).Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task On_BeforeAction_RegistersEvent()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "BeforeAction", "OnVarSet", actionPattern: "variable.set").Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task On_AfterAction_RegistersEvent()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "AfterAction", "OnAfterAction", actionPattern: "variable.*").Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task On_InvalidType_ReturnsError()
    {
        var context = CreateContext();
        var result = await MakeOn(context, "NonExistentType", "Goal").Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidEventType");
    }

    [Test]
    public async Task Remove_UnregistersEvent()
    {
        var context = CreateContext();
        var registerResult = await MakeOn(context, "BeforeGoal", "LogGoal", goalPattern: "*").Run();
        var eventId = (string)registerResult.Value!;

        await Assert.That(context.Events.Count).IsEqualTo(1);

        var removeHandler = new Remove { Context = context, EventId = eventId };
        var removeResult = await removeHandler.Run();

        await Assert.That(removeResult.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task On_WithRegex_MatchesRegexPattern()
    {
        var context = CreateContext();
        await MakeOn(context, "BeforeGoal", "LogGoal", goalPattern: "^Admin", isRegex: true).Run();

        var match = context.Events.GetMatchingBindings(EventType.BeforeGoal, goalName: "AdminGoal");
        await Assert.That(match.Count).IsEqualTo(1);

        var noMatch = context.Events.GetMatchingBindings(EventType.BeforeGoal, goalName: "UserGoal");
        await Assert.That(noMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalPattern_Wildcard_MatchesPrefix()
    {
        var context = CreateContext();
        await MakeOn(context, "BeforeGoal", "LogGoal", goalPattern: "/admin/*").Run();

        var match = context.Events.GetMatchingBindings(EventType.BeforeGoal, goalName: "/admin/Users");
        await Assert.That(match.Count).IsEqualTo(1);

        var noMatch = context.Events.GetMatchingBindings(EventType.BeforeGoal, goalName: "/public/Home");
        await Assert.That(noMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ActionPattern_Wildcard_MatchesModule()
    {
        var context = CreateContext();
        await MakeOn(context, "BeforeAction", "OnVar", actionPattern: "variable.*").Run();

        var match = context.Events.GetMatchingBindings(EventType.BeforeAction, module: "variable", actionName: "set");
        await Assert.That(match.Count).IsEqualTo(1);

        var noMatch = context.Events.GetMatchingBindings(EventType.BeforeAction, module: "file", actionName: "read");
        await Assert.That(noMatch.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PerContextIsolation_TwoContexts_DifferentEvents()
    {
        var context1 = CreateContext();
        var context2 = CreateContext();

        await MakeOn(context1, "BeforeGoal", "LogGoal", goalPattern: "TestGoal").Run();

        await Assert.That(context1.Events.Count).IsEqualTo(1);
        await Assert.That(context2.Events.Count).IsEqualTo(0);
    }
}
