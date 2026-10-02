using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// `read x, on error call Fix, write to %got%` — the step's code is [action, on.error(Recovery=…),
/// variable.set(Name=got, Value=%!data%)]: once on.error recovers, the write takes the recovery's result.
/// </summary>
public class RecoveredWriteToTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/recovered-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.goal.step.action.@this WriteTo(string name)
        => Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"),
            Make.Param(Ctx, "Value", "%!data%", new global::app.type.@this("item", template: new global::app.type.item.template.kind.plang.@this())));

    private global::app.goal.step.action.@this Throw()
        => Make.Action(Ctx, "error", "throw", ("Message", "broke"), ("Key", "Broke"));

    private async Task<global::app.goal.@this> Load(string name, params Make.StepDef[] steps)
    {
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, name, "/" + name + ".goal", steps));
        _app.goal.list.Add(goal);
        return goal;
    }

    [Test]
    public async Task ARecoveryGoalsReturn_IsWritten()
    {
        await Load("Fix", Make.Step("return fixed", Make.Action(Ctx, "goal", "return", ("Data", "fixed"))));
        var caller = await Load("Caller",
            Make.Step("throw, on error call Fix, write to %got%",
                Throw(), Make.Action(Ctx, "on", "error", Make.Recovery(Ctx, Make.Call(Ctx, "Fix"))), WriteTo("got")),
            Make.Step("set after", Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "after", "variable"), ("Value", 1))));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("got")).Value())?.ToString()).IsEqualTo("fixed");
        await Assert.That((await Ctx.Variable.Get("after")).IsInitialized).IsTrue();
    }

    [Test]
    public async Task AnInlineRecoverysValue_IsWritten()
    {
        var caller = await Load("Caller",
            Make.Step("throw, on error set %x% = fallback, write to %got%",
                Throw(),
                Make.Action(Ctx, "on", "error", Make.Recovery(Ctx,
                    Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "x", "variable"), ("Value", "fallback")))),
                WriteTo("got")));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("got")).Value())?.ToString()).IsEqualTo("fallback");
    }

    [Test]
    public async Task AnIgnoredError_WritesNothing()
    {
        var caller = await Load("Caller",
            Make.Step("throw, ignore errors, write to %got%",
                Throw(), Make.Action(Ctx, "on", "error", ("Ignore", true)), WriteTo("got")));

        await (await caller.Start(Ctx)).IsSuccess();

        var got = await Ctx.Variable.Get("got");
        await Assert.That(got.IsInitialized && got.Peek() is { IsNull: false }).IsFalse();
    }
}
