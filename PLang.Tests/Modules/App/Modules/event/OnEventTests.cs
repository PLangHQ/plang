using app.module.on;

namespace PLang.Tests.App.actions.EventTests;

// on.event binds a call on an event, reached by its path (%!app.type.goal.on.start%); the call reads %!event% (the
// running event, with %!event!item% and %!event!result% for this firing) — on the frame the event fired in, for
// exactly as long as the call runs. on.unbind takes the binding off; on.cancel, from a call bound before an event,
// cancels it with a value.
public class OnEventTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    // A program's on.event step, run: the path resolves the way a .pr's does.
    private async Task<global::app.data.@this> On(string eventPath, string when, string goal, params (string, object?)[] args)
        => await Make.Action(Ctx, "on", "event", ("Event", eventPath), ("When", when), ("Action", Make.Call(Ctx, goal, args))).Start(Ctx);

    private async Task<global::app.@event.binding.@this> Bind(string eventPath, string when, string goal, params (string, object?)[] args)
    {
        var bound = await On(eventPath, when, goal, args);
        await bound.IsSuccess();
        return (global::app.@event.binding.@this)bound.Peek();
    }

    private Goal Goal(string name, params Make.StepDef[] steps)
    {
        var goal = Make.Goal(Ctx, name, steps);
        _app.goal.list.Add(goal);
        return goal;
    }

    // A handler that keeps what it read: %name% = the value at %path%
    private Make.StepDef Keep(string name, string path)
        => Make.Step($"set %{name}% = {path}", Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", name, "variable"), ("Value", path)));

    private async Task<object?> Value(string name) => await (await Ctx.Variable.Get(name)).Value();

    [Test]
    public async Task BeforeEachGoal_BindsOnTheGoalTypesStart_ForThisActor()
    {
        var binding = await Bind("%!app.type.goal.on.start%", "before", "Log");

        var before = _app.type.list["goal"].on.start.before;
        await Assert.That(before.Count).IsEqualTo(1);
        await Assert.That(before[0]).IsSameReferenceAs(binding);
        await Assert.That(binding.Actor).IsSameReferenceAs(_app.actor.list.User);
    }

    [Test]
    public async Task BindingOneItemsEvent_LeavesEveryOtherItemUnbound()
    {
        // file.read nothing is bound on answers the shared empty events; the binding lands on its own
        await Bind("%!app.module.file.read.on.start%", "before", "Log");

        await Assert.That(_app.Module("file")["read"]!.on.start.before.Count).IsEqualTo(1);
        await Assert.That(_app.Module("file")["save"]!.on.start.before.Count).IsEqualTo(0);
        await Assert.That(_app.Module("file").on.start.before.Count).IsEqualTo(0);
        await Assert.That(_app.type.list["action"].on.start.before.Count).IsEqualTo(0);
        await Assert.That(_app.Module("output")["write"]!.on.start.before.Count).IsEqualTo(0);
    }

    // an item's event that isn't one (before/after fused into the name) can never bind: the build refuses it, naming
    // the events and the When
    [Test]
    [Arguments("%!app.type.step.on.after%")]
    [Arguments("%!app.type.step.on.before%")]
    public async Task APathNamingNoEvent_IsRefusedAtBuild_NamingTheEvents(string path)
    {
        var (handler, _) = await Make.Action(Ctx, "on", "event", ("Event", path), ("When", "before"),
            ("Action", Make.Call(Ctx, "Log"))).Bind(Ctx);

        var built = await ((global::app.module.IClass)handler!).Build();

        await built.IsFailure();
        await Assert.That(built.Error!.Key).IsEqualTo("EventNotFound");
        await Assert.That(built.Error.Message).Contains("start").And.Contains("When=before");
    }

    [Test]
    [Arguments("%!channel.audit.on.write%", "audit")]
    public async Task APathThatReachesNothingAtBuild_IsAWarning_NamingTheHop(string path, string hop)
    {
        var builder = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel("builder", builder, ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        var action = Make.Action(Ctx, "on", "event", ("Event", path), ("When", "before"), ("Action", Make.Call(Ctx, "Log")));
        var (handler, error) = await action.Bind(Ctx);
        await Assert.That(error).IsNull();

        var built = await ((global::app.module.IClass)handler!).Build();

        await built.IsSuccess();
        var written = System.Text.Encoding.UTF8.GetString(builder.ToArray());
        await Assert.That(written).Contains("reaches nothing at");
        await Assert.That(written).Contains(hop);
    }

    [Test]
    public async Task APathThatReachesAnEventAtBuild_WarnsNothing()
    {
        var builder = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel("builder", builder, ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        var (handler, _) = await Make.Action(Ctx, "on", "event", ("Event", "%!app.type.goal.on.start%"), ("When", "before"),
            ("Action", Make.Call(Ctx, "Log"))).Bind(Ctx);

        await (await ((global::app.module.IClass)handler!).Build()).IsSuccess();

        await Assert.That(builder.Length).IsEqualTo(0);
    }

    [Test]
    public async Task WhatIsNotAnEvent_IsTheError()
    {
        var bound = await On("%!app.type.goal%", "before", "Log");

        await bound.IsFailure();
        await Assert.That(bound.Error!.Key).IsEqualTo("EventNotFound");
        await Assert.That(bound.Error.Message).Contains("an item's events are ask, click, create");
    }

    // at bind, what is missing is said: a name that is no event is the events' refusal; an item that isn't there is
    // the item unreached, never "names no event"
    [Test]
    public async Task AtBind_ANameThatIsNoEvent_IsRefusedNamingTheEvents()
    {
        var bound = await On("%!app.type.step.on.before%", "before", "Log");

        await bound.IsFailure();
        await Assert.That(bound.Error!.Key).IsEqualTo("EventNotFound");
        await Assert.That(bound.Error.Message).Contains("'before' names no event").And.Contains("When=before");
    }

    [Test]
    public async Task AtBind_AnItemThatIsNotThere_IsUnreached()
    {
        var bound = await On("%!channel.audit.on.write%", "before", "Log");

        await bound.IsFailure();
        await Assert.That(bound.Error!.Key).IsEqualTo("EventUnreached");
        await Assert.That(bound.Error.Message).Contains("the item isn't there");
    }

    [Test]
    public async Task AWhenHoldingNothing_IsTheAnswer_NothingBound()
    {
        var bound = await On("%!app.type.goal.on.start%", "%unset%", "Log");

        await bound.IsFailure();
        await Assert.That(bound.Error!.Key).IsEqualTo("VariableNotFound");
        await Assert.That(_app.type.list["goal"].on.start.before.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AfterAGoal_TheCallRuns_WithItsArguments()
    {
        Goal("AfterCallback");
        Goal("MainGoal");
        await Bind("%!app.type.goal.on.start%", "after", "AfterCallback", ("callbackRan", true));

        await Make.Call(Ctx, "MainGoal").Start(Ctx);

        await Assert.That(await Value("callbackRan")).IsNotNull();
    }

    [Test]
    public async Task TheCall_ReadsTheItemItFiredFor_AndItsWritesStay_AndTheEventIsGoneAfter()
    {
        Goal("Watch", Keep("seen", "%!event!item%"));
        var target = Goal("Target");
        await Bind("%!app.goal[\"/Target\"].on.start%", "before", "Watch");

        await Make.Call(Ctx, "Target").Start(Ctx);

        await Assert.That(await Value("seen")).IsSameReferenceAs(target);
        await Assert.That(Ctx.call.Event).IsNull();
    }

    [Test]
    public async Task AfterAnAction_TheCallReadsItsResult()
    {
        Goal("Watch", Keep("seenResult", "%!event!result%"));
        await Bind("%!app.type.action.on.start%", "after", "Watch");

        var main = Goal("Main", Make.Step("set %x% = one", Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "x", "variable"), ("Value", "one"))));
        await Make.Call(Ctx, "Main").Start(Ctx);

        await Assert.That((await Value("seenResult"))?.ToString()).IsEqualTo("one");
    }

    [Test]
    public async Task ANestedEventsCall_SeesItsOwnEvent_AndTheOuterCallItsOwnAgain()
    {
        // before Outer starts, Watch runs: it calls Inner (whose own before-start runs InnerWatch), then reads again
        Goal("InnerWatch", Keep("innerSeen", "%!event!item%"));
        var inner = Goal("Inner");
        Goal("Watch",
            Keep("outerBefore", "%!event!item%"),
            Make.Step("call Inner", Make.Call(Ctx, "Inner")),
            Keep("outerAfter", "%!event!item%"));
        var outer = Goal("Outer");
        await Bind("%!app.goal[\"/Outer\"].on.start%", "before", "Watch");
        await Bind("%!app.goal[\"/Inner\"].on.start%", "before", "InnerWatch");

        await Make.Call(Ctx, "Outer").Start(Ctx);

        await Assert.That(await Value("outerBefore")).IsSameReferenceAs(outer);
        await Assert.That(await Value("innerSeen")).IsSameReferenceAs(inner);
        await Assert.That(await Value("outerAfter")).IsSameReferenceAs(outer);
    }

    [Test]
    public async Task TwoParallelFirings_EachCallSeesItsOwnEvent()
    {
        // each handler waits between being handed its event and reading it — a shared slot would cross them
        var sleep = Make.Step("wait", Make.Action(Ctx, "timer", "sleep", ("Ms", 50)));
        Goal("WatchA", sleep, Keep("seenA", "%!event!item%"));
        Goal("WatchB", sleep, Keep("seenB", "%!event!item%"));
        var a = Goal("A");
        var b = Goal("B");
        await Bind("%!app.goal[\"/A\"].on.start%", "before", "WatchA");
        await Bind("%!app.goal[\"/B\"].on.start%", "before", "WatchB");

        await Task.WhenAll(Make.Call(Ctx, "A").Start(Ctx), Make.Call(Ctx, "B").Start(Ctx));

        await Assert.That(await Value("seenA")).IsSameReferenceAs(a);
        await Assert.That(await Value("seenB")).IsSameReferenceAs(b);
    }

    [Test]
    public async Task Unbind_TakesTheBindingOff()
    {
        var binding = await Bind("%!app.type.goal.on.start%", "before", "Log");

        var unbound = await new OnUnbind(Ctx)
        {
            Binding = new global::app.data.@this<global::app.@event.binding.@this>("Binding", binding, context: Ctx),
        }.Start();

        await unbound.IsSuccess();
        await Assert.That(_app.type.list["goal"].on.start.before.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ABinding_FiresOnlyWhileItsActorRuns()
    {
        Goal("Watch", Keep("seen", "%watched%"));
        Goal("Target");
        await Bind("%!app.goal[\"/Target\"].on.start%", "before", "Watch", ("watched", true));

        await Make.Call(Ctx, "Target").Start(_app.actor.list.System.Context);
        await Assert.That((await _app.actor.list.System.Context.Variable.Get("seen")).IsInitialized).IsFalse();

        // the same goal under the actor that bound it: it fires
        await Make.Call(Ctx, "Target").Start(Ctx);
        await Assert.That((await Ctx.Variable.Get("seen")).IsInitialized).IsTrue();
    }

    [Test]
    public async Task Cancel_AnswersItsValue_MarkedHandled()
    {
        var cancelled = await new OnCancel(Ctx) { Value = new global::app.data.@this("", "override-value", context: Ctx) }.Start();

        await cancelled.IsSuccess();
        await Assert.That(cancelled.Handled).IsTrue();
        await Assert.That((await cancelled.Value())?.ToString()).IsEqualTo("override-value");
    }

    [Test]
    public async Task Cancel_WithNoValue_AnswersEmpty_MarkedHandled()
    {
        var cancelled = await new OnCancel(Ctx).Start();

        await cancelled.IsSuccess();
        await Assert.That(cancelled.Handled).IsTrue();
    }
}
