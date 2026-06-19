using app.actor.context;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 6 — AfterAction event payload widening.
/// Today: lifecycle.After.Run(context, Trigger.AfterAction).
/// After:  lifecycle.After.Run(context, Trigger.AfterAction, this, result).
/// Subscribers now receive (Context, Action, Data) — unlocking module.action coverage
/// and branch coverage without touching the Data type itself. All call sites and
/// subscribers updated in the same commit; no backward-compat shim.
/// v1 widens only AfterAction; BeforeAction stays as-is.
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

    // Runs a simple goal with one action (variable.set) so a single AfterAction fires.
    private async Task RunSimpleGoal(string varName = "x", int value = 42)
    {
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("TestGoal",
            Make.Step("set var",
                Make.Action("variable", "set", Make.Param("Name", varName, "variable"), ("Value", value)))));
        _app.Goal.Add(goal);
        await _app.RunGoalAsync(goal, _app.User.Context);
    }

    // Subscribers to AfterAction receive the Action that just ran — Action.Module,
    // .ActionName, .Step, .Goal all accessible from the payload.
    [Test]
    public async Task AfterAction_Fires_PassesActionInstanceInPayload()
    {
        PrAction? captured = null;
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) => { captured = action; return Task.FromResult(Data.Ok()); },
            priority: int.MaxValue,
            stopOnError: false));

        await RunSimpleGoal();

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Module).IsEqualTo("variable");
        await Assert.That(captured.ActionName).IsEqualTo("set");
    }

    // Subscribers receive the Data the action returned — Data.Value, .Properties,
    // .Error, .Success all readable for coverage and branch tracking.
    [Test]
    public async Task AfterAction_Fires_PassesResultDataInPayload()
    {
        Data? captured = null;
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) => { captured = result; return Task.FromResult(Data.Ok()); },
            priority: int.MaxValue,
            stopOnError: false));

        await RunSimpleGoal();

        await Assert.That(captured).IsNotNull();
        await captured!.IsSuccess();
    }

    // timeout.after wrapping http.request emits two AfterAction events — one for the
    // modifier itself, one for the inner action. Confirms coverage inventory includes
    // modifiers (architect §5.6). Each Action.RunAsync fires its own AfterAction.
    [Test]
    public async Task AfterAction_ForModifierAction_FiresSeparatelyFromInnerAction()
    {
        // Simpler fixture: variable.set wrapped by timeout.after. The modifier and the
        // inner variable.set each go through Action.RunAsync → fire their own AfterAction.
        var inner = Make.Modified(
            Make.Action("variable", "set", Make.Param("Name", "y", "variable"), ("Value", 7)),
            Make.Action("timeout", "after", ("Ms", 5000)));

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("ModifierGoal",
            Make.Step("mod set", inner)));
        _app.Goal.Add(goal);

        var observed = new List<(string Module, string ActionName)>();
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) =>
            {
                if (action != null) observed.Add((action.Module, action.ActionName));
                return Task.FromResult(Data.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false));

        await _app.RunGoalAsync(goal, _app.User.Context);

        // Exact count and order — Modifiers.RunAsync emits the modifier's AfterAction
        // first (inside its own post-loop), then control returns to Action.RunAsync which
        // emits the inner action's AfterAction. Duplicate firings would corrupt
        // coverage counts silently; unexpected order signals a lifecycle regression.
        await Assert.That(observed.Count).IsEqualTo(2);
        await Assert.That(observed[0]).IsEqualTo(("timeout", "after"));
        await Assert.That(observed[1]).IsEqualTo(("variable", "set"));
    }

    // Regression guard: architect widened only AfterAction. BeforeAction stays at
    // (context, Trigger). If BeforeAction is widened in the future, this test flags
    // it as an intentional scope change.
    [Test]
    public async Task BeforeAction_SignatureUnchanged_NoPayloadWidening()
    {
        // BeforeAction subscribers see null action/result — the emit site doesn't pass them.
        // All other events share the same Handler type, but only AfterAction gets the payload.
        PrAction? seenAction = null;
        Data? seenResult = null;
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.BeforeAction,
            (context, action, result) =>
            {
                seenAction = action;
                seenResult = result;
                return Task.FromResult(Data.Ok());
            },
            priority: int.MaxValue,
            stopOnError: false));

        await RunSimpleGoal();

        await Assert.That(seenAction).IsNull();
        await Assert.That(seenResult).IsNull();
    }

    // Failed action (data.Success == false) still triggers AfterAction — the error is
    // visible to the user so the action "threw" from their perspective, and coverage
    // tracks attempted execution. (independent — architect flagged as open question §5.6)
    [Test]
    public async Task AfterAction_OnActionFailure_FiresWithErrorData()
    {
        // Build a goal whose action will fail: assert.equals with mismatched values.
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("FailGoal",
            Make.Step("bad assert",
                Make.Action("assert", "equals", ("Expected", 1), ("Actual", 2)))));
        _app.Goal.Add(goal);

        Data? captured = null;
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) => { captured = result; return Task.FromResult(Data.Ok()); },
            priority: int.MaxValue,
            stopOnError: false));

        await _app.RunGoalAsync(goal, _app.User.Context);

        await Assert.That(captured).IsNotNull();
        await captured!.IsFailure();
        await Assert.That(captured.Error).IsNotNull();
    }

    // Action.Step.Goal navigation works from the payload — branch coverage keys sites
    // as "goalName:stepIndex", which requires the Action to carry Step + Goal refs.
    [Test]
    public async Task AfterAction_Payload_ActionCarriesStepAndGoalForSiteKey()
    {
        PrAction? captured = null;
        _app.User.Context.Events.Register(new EventBinding(
            Trigger.AfterAction,
            (context, action, result) => { captured = action; return Task.FromResult(Data.Ok()); },
            priority: int.MaxValue,
            stopOnError: false));

        await RunSimpleGoal();

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Step).IsNotNull();
        await Assert.That(captured.Step!.Goal).IsNotNull();
        await Assert.That(captured.Step.Goal!.Name).IsEqualTo("TestGoal");
        await Assert.That(captured.Step.Index).IsEqualTo(0);
    }
}
