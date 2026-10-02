using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// `call X, don't wait` (goal.call Wait=false): the goal runs on its own and the step goes on at once; what it fails
/// with goes to its actor's error channel — nothing else would see it. The actor named still runs it.
/// </summary>
public class CallWaitTests
{
    private global::app.@this _app = null!;
    private System.IO.MemoryStream _errors = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/tmp/callwait-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        _errors = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Error, _errors, global::app.channel.ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain", Framed = true });
    }

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

    private global::app.goal.step.action.@this NoWait(string goal, params (string name, object? value)[] more)
        => Make.Action(Ctx, "goal", "call", new[] { ("Name", (object?)goal), ("Wait", (object?)false) }.Concat(more).ToArray());

    // a condition met within a few seconds
    private static async Task<bool> Soon(System.Func<Task<bool>> met)
    {
        for (var i = 0; i < 100; i++) { if (await met()) return true; await Task.Delay(50); }
        return false;
    }

    [Test]
    public async Task TheStep_GoesOnBeforeTheGoalEnds()
    {
        await Load("Slow", Make.Step("sleep", Make.Action(Ctx, "timer", "sleep", ("Ms", 500))), Make.Step("set done", Set("done", 1)));
        var caller = await Load("Caller", Make.Step("call Slow, don't wait", NoWait("Slow")));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await Ctx.Variable.Get("done")).IsInitialized).IsFalse();
        await Assert.That(await Soon(async () => (await Ctx.Variable.Get("done")).IsInitialized)).IsTrue();
    }

    [Test]
    public async Task AFailure_NothingWaitsFor_GoesToTheErrorChannel()
    {
        await Load("Fails", Make.Step("throw", Make.Action(Ctx, "error", "throw", ("Message", "the background broke"), ("Key", "BackgroundBroke"))));
        var caller = await Load("Caller", Make.Step("call Fails, don't wait", NoWait("Fails")));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That(await Soon(() => Task.FromResult(System.Text.Encoding.UTF8.GetString(_errors.ToArray()).Contains("the background broke")))).IsTrue();
    }

    [Test]
    public async Task AnErrorChannelThatCantTakeIt_FallsBackToTheSystems()
    {
        var closed = new System.IO.MemoryStream();
        closed.Dispose();
        _app.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Error, closed, global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        var system = new System.IO.MemoryStream();
        _app.actor.list.System.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Error, system, global::app.channel.ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain", Framed = true });

        await Ctx.Actor.Channel.Report(Ctx.Error(new global::app.error.Error("nobody waited", "Unwaited", 500)));

        await Assert.That(System.Text.Encoding.UTF8.GetString(system.ToArray())).Contains("nobody waited");
    }

    // an error goal channel's goal gets the failure as %message% and where it failed as %where.goal% / %where.step%
    [Test]
    public async Task AnErrorGoalChannel_GetsTheFailureAndWhereItFailed()
    {
        var fails = await Load("Fails", Make.Step("throw it", Make.Action(Ctx, "error", "throw", ("Message", "it broke"), ("Key", "Broke"))));
        await Load("Shown",
            Make.Step("set seen goal", Set("seenGoal", "%where.goal%")),
            Make.Step("set seen step", Set("seenStep", "%where.step%")));
        await _app.actor.list.User.Channel.Set(await Make.GoalChannel(global::app.channel.list.@this.Error,
            Make.Call(Ctx, "Shown"), _app.actor.list.User));

        var failed = await fails.Start(Ctx);
        await failed.IsFailure();
        await Ctx.Actor.Channel.Report(failed);

        await Assert.That((await (await Ctx.Variable.Get("seenGoal")).Value())?.ToString()).IsEqualTo("Fails");
        await Assert.That((await (await Ctx.Variable.Get("seenStep")).Value())?.ToString()).IsEqualTo("throw it");
    }

    [Test]
    public async Task TheActorNamed_RunsIt()
    {
        await Load("Mark", Make.Step("set ran", Set("ran", 1)));
        var caller = await Load("Caller", Make.Step("call Mark on system, don't wait", NoWait("Mark", ("Actor", "system"))));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That(await Soon(async () => (await _app.actor.list.System.Context.Variable.Get("ran")).IsInitialized)).IsTrue();
        await Assert.That((await Ctx.Variable.Get("ran")).IsInitialized).IsFalse();
    }

    // the parameters given as one value at run — a dict, each entry a named row (what an agent asked for)
    [Test]
    public async Task ParametersGivenAsADict_BindEachEntry()
    {
        await Ctx.Variable.Set("asked", new Dictionary<string, object?> { ["city"] = "Reykjavik", ["days"] = 3 });
        await Load("Weather", Make.Step("set got", Set("got", "%city% for %days%")));
        var caller = await Load("Caller", Make.Step("call Weather with %asked%",
            Make.Action(Ctx, "goal", "call", ("Name", "Weather"),
                Make.Param(Ctx, "Parameter", "%asked%", new global::app.type.@this("list", template: "plang")))));

        await (await caller.Start(Ctx)).IsSuccess();

        await Assert.That((await (await Ctx.Variable.Get("got")).Value())?.ToString()).IsEqualTo("Reykjavik for 3");
    }

    // a plain list slot given a dict converts as it always has (to no rows) — only a goal call reads a dict's entries
    // as its named rows
    [Test]
    public async Task APlainListGivenADict_ConvertsAsBefore()
    {
        var ctx = Ctx;
        var dict = ctx.Ok(global::app.type.item.@this.Create(new Dictionary<string, object?> { ["a"] = 1 }, ctx));
        var asList = await dict.Value<global::app.type.item.list.@this>();
        await Assert.That(asList).IsNotNull();
        await Assert.That(asList!.CountRaw).IsEqualTo(0);
    }

    [Test]
    public async Task AnOlderPr_ThatSaysWaitForExecution_StillReads()
    {
        var pr = """{"name":"Old","path":"/Old.goal","step":[{"index":0,"text":"a","code":[],"waitForExecution":true}]}""";
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.Read(_app, pr);
        await Assert.That(goal.Step[0].Text).IsEqualTo("a");
    }
}
