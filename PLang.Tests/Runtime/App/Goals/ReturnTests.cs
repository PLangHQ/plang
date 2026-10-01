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
    public async Task ReturnMissing_FailsAsReadingTheVariableFails()
    {
        var callee = await Load("Callee",
            Make.Step("return %missing%", Return("%missing%")),
            Make.Step("set %after%", Set("after", 1)));
        var returned = await callee.Start(Ctx);
        await returned.IsFailure();
        await Assert.That(returned.Error!.Key).IsEqualTo("VariableNotFound");
        await Assert.That((await Ctx.Variable.Get("after")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task ReturnMissing_IsCaughtByTheStepsOnError()
    {
        var callee = await Load("Callee",
            Make.Step("return %missing%, on error set %caught%",
                Return("%missing%"), Make.Action(Ctx, "on", "error", Make.Recovery(Ctx, Set("caught", true)))));
        await callee.Start(Ctx);
        await Assert.That((await Ctx.Variable.Get("caught")).IsInitialized).IsTrue();
    }

    [Test]
    public async Task SetToAMissingVariable_LeavesItUnset_AndALaterSetDoesNotReviveIt()
    {
        await Ctx.Variable.Set("y", new global::app.data.@this("value", "%missing%",
            Ctx.App.type.list[new global::app.type.@this("text", template: "plang"), Ctx], context: Ctx));
        await Assert.That((await Ctx.Variable.Get("y")).IsInitialized).IsFalse();

        await Ctx.Variable.Set("missing", 5);
        await Assert.That((await Ctx.Variable.Get("y")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task SetToAVariableHoldingAFailure_KeepsTheFailure()
    {
        await Ctx.Variable.Set("r", Ctx.Error(new global::app.error.Error("the call failed", "CallFailed", 500)));
        await Ctx.Variable.Set("y", new global::app.data.@this("value", "%r%",
            Ctx.App.type.list[new global::app.type.@this("text", template: "plang"), Ctx], context: Ctx));
        var y = await Ctx.Variable.Get("y");
        await Assert.That(y.IsInitialized).IsTrue();
        await Assert.That(y.Error?.Key).IsEqualTo("CallFailed");
    }

    private global::app.data.@this Template(string text)
        => new("value", text, Ctx.App.type.list[new global::app.type.@this("text", template: "plang"), Ctx], context: Ctx);

    [Test]
    public async Task AReturnedTemplate_RendersWhereItIsWritten()
    {
        await Load("Callee", Make.Step("return \"%name%: %price% kr\"", Return("%name%: %price% kr")));
        var got = await Got(("name", "milk"), ("price", 3));
        await Assert.That((await got.Value())?.ToString()).IsEqualTo("milk: 3 kr");
    }

    [Test]
    public async Task AReturnedVariable_SetFromATemplate_HoldsWhatItRendered()
    {
        await Load("Callee",
            Make.Step("set %text% = \"%name%: %price% kr\"", Set("text", "%name%: %price% kr")),
            Make.Step("return %text%", Return("%text%")));
        var got = await Got(("name", "milk"), ("price", 3));
        await Assert.That((await got.Value())?.ToString()).IsEqualTo("milk: 3 kr");
    }

    [Test]
    public async Task ATemplateSet_RendersAtTheSet_AndALaterSetDoesNotChangeIt()
    {
        await Ctx.Variable.Set("n", "a");
        await Ctx.Variable.Set("t", Template("Hi %n%"));
        await Ctx.Variable.Set("n", "x");
        await Assert.That((await (await Ctx.Variable.Get("t")).Value())?.ToString()).IsEqualTo("Hi a");
    }

    [Test]
    public async Task ATemplateThatAppendsToItself_Accumulates()
    {
        await Ctx.Variable.Set("order", "start");
        await Ctx.Variable.Set("order", Template("%order%,low"));
        await Ctx.Variable.Set("order", Template("%order%,low"));
        await Assert.That((await (await Ctx.Variable.Get("order")).Value())?.ToString()).IsEqualTo("start,low,low");
    }

    [Test]
    public async Task ATemplateNamingAMissingVariable_FailsTheSet()
    {
        var set = await Ctx.Variable.Set("t", Template("Hi %nobody%"));
        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("VariableNotFound");
    }

    [Test]
    public async Task ATemplateParameter_RendersWithTheCallersVariables()
    {
        await Ctx.Variable.Set("first", "Ada");
        await Ctx.Variable.Set("last", "Lovelace");
        await Load("Callee", Make.Step("return %name%", Return("%name%")));
        var got = await Got(("name", "%first% %last%"));
        await Assert.That((await got.Value())?.ToString()).IsEqualTo("Ada Lovelace");
    }

    [Test]
    public async Task ATemplateParameter_IsTheCallers_EvenWhenTheCalleeHasItsOwnVariableOfThatName()
    {
        await Ctx.Variable.Set("first", "Ada");
        await Ctx.Variable.Set("last", "Lovelace");
        await Load("Callee",
            Make.Step("set %first% = \"callee\"", Set("first", "callee")),
            Make.Step("return %name%", Return("%name%")));
        var got = await Got(("name", "%first% %last%"));
        await Assert.That((await got.Value())?.ToString()).IsEqualTo("Ada Lovelace");
    }

    // A parameter reads its value as a set does: a reference to nothing leaves it unset, a template that can't
    // render fails the call, a variable holding a failure fails it with that failure.
    [Test]
    public async Task AParameterNamingAnUnsetVariable_DoesNotFailTheCall()
    {
        await Load("Callee", Make.Step("return \"ok\"", Return("ok")));
        var caller = await Load("Caller", Make.Step("call Callee name=%missing%", Make.Call(Ctx, "Callee", ("name", "%missing%"))));

        await (await caller.Start(Ctx)).IsSuccess();
    }

    [Test]
    public async Task AParameterNamingAnUnsetVariable_IsUnsetInTheCallee_NotTheCallersOfThatName()
    {
        await Ctx.Variable.Set("name", "outer");
        await Load("Callee", Make.Step("return %name%", Return("%name%")));
        var caller = await Load("Caller", Make.Step("call Callee name=%missing%", Make.Call(Ctx, "Callee", ("name", "%missing%"))));

        var result = await caller.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }

    [Test]
    public async Task AParameterTemplateNamingAnUnsetVariable_FailsTheCall()
    {
        await Load("Callee", Make.Step("return %name%", Return("%name%")));
        var caller = await Load("Caller", Make.Step("call Callee name=\"Hi %missing%\"", Make.Call(Ctx, "Callee", ("name", "Hi %missing%"))));

        var result = await caller.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }

    [Test]
    public async Task AParameterNamingAVariableHoldingAFailure_FailsTheCallWithIt()
    {
        await Ctx.Variable.Set("r", Ctx.Error(new global::app.error.Error("the call failed", "CallFailed", 500)));
        await Load("Callee", Make.Step("return %x%", Return("%x%")));
        var caller = await Load("Caller", Make.Step("call Callee x=%r%", Make.Call(Ctx, "Callee", ("x", "%r%"))));

        var result = await caller.Start(Ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CallFailed");
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
