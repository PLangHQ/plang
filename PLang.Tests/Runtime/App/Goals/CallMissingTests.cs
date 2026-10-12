using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// A call to a goal that isn't there fails GoalNotFound, an error like any other: the step's own <c>on error</c>
/// catches it — a recovery that sets a variable, or one that calls a goal.
/// </summary>
public class CallMissingTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/call-missing-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.goal.@this> Load(string name, params Make.StepDef[] steps)
    {
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, name, "/" + name + ".goal", steps));
        _app.goal.list.Add(goal);
        return goal;
    }

    private global::app.goal.step.action.@this Set(string name, object? value)
        => Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"), ("Value", value));

    [Test]
    public async Task AMissingGoal_IsCaughtByTheStepsOnError()
    {
        var caller = await Load("Caller",
            Make.Step("call NonExistentGoal, on error set %caught% = %!error.Key%",
                Make.Call(Ctx, "NonExistentGoal"), Make.Action(Ctx, "on", "error", Make.Recovery(Ctx, Set("caught", "%!error.Key%")))));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("caught")).Value())?.ToString()).IsEqualTo("GoalNotFound");
    }

    [Test]
    public async Task AMissingGoal_IsCaughtByARecoveryThatCallsAGoal()
    {
        await Load("HandleMissing", Make.Step("set %missingCaught% = true", Set("missingCaught", true)));
        var caller = await Load("Caller",
            Make.Step("call NonExistentGoal, on error call HandleMissing",
                Make.Call(Ctx, "NonExistentGoal"), Make.Action(Ctx, "on", "error", Make.Recovery(Ctx, Make.Call(Ctx, "HandleMissing")))));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("missingCaught")).Value())?.ToString()).IsEqualTo("true");
    }
}
