using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Shortcut;

/// <summary>
/// The place in play reads through the system's shortcuts (<c>/system/shortcut/</c>, the os's): <c>%!goal%</c>,
/// <c>%!step%</c>, <c>%!error%</c>, <c>%!test%</c>, <c>%!channel%</c> — each its asker's, and a read leaves the
/// asker's <c>%!data%</c> as it was.
/// </summary>
public class SystemShortcutTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/sysshortcut-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.goal.step.action.@this Set(string name, string value)
        => Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"),
            Make.Param(Ctx, "Value", value, new global::app.type.@this("item", template: "plang")));

    private async Task<global::app.goal.@this> Load(string name, params Make.StepDef[] steps)
    {
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, name, "/" + name + ".goal", steps));
        _app.goal.list.Add(goal);
        return goal;
    }

    [Test]
    public async Task TheGoalAndStep_AreTheAskers()
    {
        var caller = await Load("Caller",
            Make.Step("set goal", Set("got", "%!goal%")),
            Make.Step("set step", Set("step", "%!step%")));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await Ctx.Variable.Get("got")).Peek()).IsSameReferenceAs(caller);
        await Assert.That((await Ctx.Variable.Get("step")).Peek()).IsSameReferenceAs(caller.Step[1]);
    }

    [Test]
    public async Task TheChannel_IsTheAskersActors()
    {
        var caller = await Load("Caller", Make.Step("set output", Set("out", "%!channel.output%")));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That(((global::app.channel.@this)(await Ctx.Variable.Get("out")).Peek()).Name).IsEqualTo("output");
    }

    [Test]
    public async Task TheError_IsTheOneInPlay_WhileItsRecoveryRuns()
    {
        await Load("Recover", Make.Step("set seen", Set("seen", "%!error.Key%")));
        var caller = await Load("Caller", Make.Step("throw, on error call Recover",
            Make.Action(Ctx, "error", "throw", ("Message", "broke"), ("Key", "Broke")),
            Make.Action(Ctx, "on", "error", Make.Recovery(Ctx, Make.Call(Ctx, "Recover")))));

        await caller.Start(Ctx);

        await Assert.That((await (await Ctx.Variable.Get("seen")).Value())?.ToString()).IsEqualTo("Broke");
    }

    // a read of %!goal% runs the shortcut goal, whose return would write %!data%: the asker's stays as it was
    [Test]
    [Arguments("%!goal%")]
    [Arguments("%!error%")]
    public async Task ReadingAShortcut_LeavesTheAskersDataAsItWas(string shortcut)
    {
        await Ctx.Variable.Set("!data", "before");

        await Make.Built(Ctx, "value", shortcut).Value();

        await Assert.That((await Ctx.Variable.Get("!data")).GetValue<string>()).IsEqualTo("before");
    }
}
