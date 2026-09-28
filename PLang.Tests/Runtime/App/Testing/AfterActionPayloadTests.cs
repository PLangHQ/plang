using app.actor.context;

namespace PLang.Tests.App.Tester;

/// <summary>
/// What a binding on the action type's <c>on.start</c> is handed: the action that started, and the result as it
/// stands — the action's result after it, for coverage and branch tracking.
/// </summary>
public class AfterActionPayloadTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // Binds handler on the action type's start, before or after, for the User actor.
    private void Bind(global::app.@event.When when,
        Func<global::app.type.item.@this, Data, global::app.actor.context.@this, Task<Data>> handler)
        => _app.type.list["action"].Own().Bind("start", when, handler, _app.User, global::app.@event.binding.Scope.actor);

    // Runs a simple goal with one action (variable.set) so a single action start fires.
    private async Task RunSimpleGoal(string varName = "x", int value = 42)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("TestGoal",
            Make.Step("set var",
                Make.Action("variable", "set", Make.Param("Name", varName, "variable"), ("Value", value)))));
        _app.goal.list.Add(goal);
        await _app.Start(goal, _app.User.Context);
    }

    // An after-binding is handed the Action that just ran — Action.Module, .Name, .Step, .Goal all accessible.
    [Test]
    public async Task AfterAction_Fires_PassesActionInstanceInPayload()
    {
        PrAction? captured = null;
        Bind(global::app.@event.When.after, (item, _, context) => { captured = item as PrAction; return Task.FromResult(context.Ok()); });

        await RunSimpleGoal();

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Module.Name).IsEqualTo("variable");
        await Assert.That(captured.Name).IsEqualTo("set");
    }

    // An after-binding is handed the Data the action returned.
    [Test]
    public async Task AfterAction_Fires_PassesResultDataInPayload()
    {
        Data? captured = null;
        Bind(global::app.@event.When.after, (_, result, context) => { captured = result; return Task.FromResult(context.Ok()); });

        await RunSimpleGoal();

        await Assert.That(captured).IsNotNull();
        await captured!.IsSuccess();
    }

    // variable.set with an on.timeout clause: the clause is bound, never started — the action type's after fires
    // once, for variable.set (the clause is covered when its action starts).
    [Test]
    public async Task AfterAction_AClause_IsNeverStarted_OnlyItsActionFires()
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("ModifierGoal",
            Make.Step("mod set",
                Make.Action("variable", "set", Make.Param("Name", "y", "variable"), ("Value", 7)),
                Make.Action("on", "timeout", ("After", System.TimeSpan.FromSeconds(5))))));
        _app.goal.list.Add(goal);

        var observed = new List<(string Module, string Name)>();
        Bind(global::app.@event.When.after, (item, _, context) =>
        {
            if (item is PrAction action) observed.Add((action.Module.Name, action.Name));
            return Task.FromResult(context.Ok());
        });

        await _app.Start(goal, _app.User.Context);

        // Exact count — duplicate firings would corrupt coverage counts silently.
        await Assert.That(observed.Count).IsEqualTo(1);
        await Assert.That(observed[0]).IsEqualTo(("variable", "set"));
    }

    // A before-binding is handed the action about to run, and the result as it stands: a plain success.
    [Test]
    public async Task BeforeAction_IsHandedTheAction_AndTheResultAsItStands()
    {
        PrAction? seenAction = null;
        Data? seenResult = null;
        Bind(global::app.@event.When.before, (item, result, context) =>
        {
            seenAction = item as PrAction;
            seenResult = result;
            return Task.FromResult(context.Ok());
        });

        await RunSimpleGoal();

        await Assert.That(seenAction?.Name).IsEqualTo("set");
        await seenResult!.IsSuccess();
    }

    // A failed action still fires its after — coverage tracks attempted execution, and the binding sees the error.
    [Test]
    public async Task AfterAction_OnActionFailure_FiresWithErrorData()
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("FailGoal",
            Make.Step("bad assert",
                Make.Action("assert", "equals", ("Expected", 1), ("Actual", 2)))));
        _app.goal.list.Add(goal);

        Data? captured = null;
        Bind(global::app.@event.When.after, (_, result, context) => { captured = result; return Task.FromResult(context.Ok()); });

        await _app.Start(goal, _app.User.Context);

        await Assert.That(captured).IsNotNull();
        await captured!.IsFailure();
        await Assert.That(captured.Error).IsNotNull();
    }

    // Action.Step.Goal navigation works from the action handed over — branch coverage keys sites as
    // "goalName:stepIndex".
    [Test]
    public async Task AfterAction_Payload_ActionCarriesStepAndGoalForSiteKey()
    {
        PrAction? captured = null;
        Bind(global::app.@event.When.after, (item, _, context) => { captured = item as PrAction; return Task.FromResult(context.Ok()); });

        await RunSimpleGoal();

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Step).IsNotNull();
        await Assert.That(captured.Step!.Goal).IsNotNull();
        await Assert.That(captured.Step.Goal!.Name).IsEqualTo("TestGoal");
        await Assert.That(captured.Step.Index).IsEqualTo(0);
    }
}
