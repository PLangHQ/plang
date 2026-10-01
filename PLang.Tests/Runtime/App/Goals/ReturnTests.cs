using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// A value is read where it is written: what a goal returns is read in the goal's own frame, before the frame is
/// gone. <c>return %!goal%</c> in a callee hands its caller the callee — a pointer to that goal; <c>%Now%</c>
/// returned is the moment of the return; a callee's argument returned is the callee's. Nothing is read through a
/// value door: a returned reference to content not yet read stays unread.
/// </summary>
public class ReturnTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/return-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.goal.@this> Load(string name, params Make.StepDef[] steps)
    {
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app, Make.Goal(Ctx, name, "/" + name + ".goal", steps));
        _app.goal.list.Add(goal);
        return goal;
    }

    private global::app.goal.step.action.@this Return(string value) => Make.Action(Ctx, "goal", "return", ("Data", value));

    private global::app.goal.step.action.@this Set(string name, object? value)
        => Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"), ("Value", value));

    // Caller: call Callee (with its arguments), write to %got%.
    private async Task<global::app.data.@this> Got(params (string name, object? value)[] arguments)
    {
        var caller = await Load("Caller",
            Make.Step("call Callee, write to %got%", Make.Call(Ctx, "Callee", arguments), Set("got", "%!data%")));
        await (await caller.Start(Ctx)).IsSuccess();
        return await Ctx.Variable.Get("got");
    }

    [Test]
    public async Task ReturnGoal_GivesTheCallerTheCallee()
    {
        var callee = await Load("Callee", Make.Step("return %!goal%", Return("%!goal%")));
        var got = await Got();
        await Assert.That(got.Peek()).IsSameReferenceAs(callee);
    }

    [Test]
    public async Task ReturnStep_GivesTheCallerTheCalleesStep()
    {
        var callee = await Load("Callee", Make.Step("return %!step%", Return("%!step%")));
        var got = await Got();
        await Assert.That(got.Peek()).IsSameReferenceAs(callee.Step[0]);
    }

    [Test]
    public async Task ReturnNow_IsTheMomentOfTheReturn()
    {
        var callee = await Load("Callee", Make.Step("return %Now%", Return("%Now%")));
        var returned = await callee.Start(Ctx);
        var first = await returned.Value();
        await Task.Delay(30);
        await Assert.That((await returned.Value())?.ToString()).IsEqualTo(first?.ToString());
    }

    [Test]
    public async Task ReturnArgument_IsTheCalleesArgument()
    {
        await Ctx.Variable.Set("city", "caller's");
        await Load("Callee", Make.Step("return %city%", Return("%city%")));
        var got = await Got(("city", "callee's"));
        await Assert.That((await got.Value())?.ToString()).IsEqualTo("callee's");
    }

    [Test]
    public async Task TheCallersData_AfterTheCall_IsWhatTheCalleeReturned()
    {
        var callee = await Load("Callee", Make.Step("return %!goal%", Return("%!goal%")));
        var caller = await Load("Caller", Make.Step("call Callee", Make.Call(Ctx, "Callee")));
        await (await caller.Start(Ctx)).IsSuccess();
        await Assert.That((await Ctx.Variable.Get("!data")).Peek()).IsSameReferenceAs(callee);
    }

    [Test]
    public async Task AReturnedReference_ToContentNotRead_StaysUnread()
    {
        var http = new global::app.type.item.path.http.@this("http://example.com/data.json");
        var read = await new global::app.module.file.Read(Ctx) { Path = new global::app.data.@this<global::app.type.item.path.@this>("", http) }.Start();
        await read.IsSuccess();
        var doc = (global::app.type.item.url.@this)read.Peek()!;
        await Ctx.Variable.Set("doc", read);
        await Load("Callee", Make.Step("return %doc%", Return("%doc%")));
        var got = await Got();
        await Assert.That(got.Peek()).IsSameReferenceAs(doc);
        await Assert.That(doc.IsLoaded).IsFalse();
    }
}
