namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 6 — AfterAction event payload widening.
/// Today: lifecycle.After.Run(context, EventType.AfterAction).
/// After:  lifecycle.After.Run(context, EventType.AfterAction, this, result).
/// Subscribers now receive (Context, Action, Data) — unlocking module.action coverage
/// and branch coverage without touching the Data type itself. All call sites and
/// subscribers updated in the same commit; no backward-compat shim.
/// v1 widens only AfterAction; BeforeAction stays as-is.
/// </summary>
public class AfterActionPayloadTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // Subscribers to AfterAction receive the Action that just ran — Action.Module,
    // .ActionName, .Step, .Goal all accessible from the payload.
    [Test]
    public async Task AfterAction_Fires_PassesActionInstanceInPayload()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Subscribers receive the Data the action returned — Data.Value, .Properties,
    // .Error, .Success all readable for coverage and branch tracking.
    [Test]
    public async Task AfterAction_Fires_PassesResultDataInPayload()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // timeout.after wrapping http.request emits two AfterAction events — one for the
    // modifier itself, one for the inner action. Confirms coverage inventory includes
    // modifiers (architect §5.6). Each Action.RunAsync fires its own AfterAction.
    [Test]
    public async Task AfterAction_ForModifierAction_FiresSeparatelyFromInnerAction()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Regression guard: architect widened only AfterAction. BeforeAction stays at
    // (context, EventType). If BeforeAction is widened in the future, this test flags
    // it as an intentional scope change.
    [Test]
    public async Task BeforeAction_SignatureUnchanged_NoPayloadWidening()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Failed action (Data.Success == false) still triggers AfterAction — the error is
    // visible to the user so the action "threw" from their perspective, and coverage
    // tracks attempted execution. (independent — architect flagged as open question §5.6)
    [Test]
    public async Task AfterAction_OnActionFailure_FiresWithErrorData()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Action.Step.Goal navigation works from the payload — branch coverage keys sites
    // as "goalName:stepIndex", which requires the Action to carry Step + Goal refs.
    [Test]
    public async Task AfterAction_Payload_ActionCarriesStepAndGoalForSiteKey()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
