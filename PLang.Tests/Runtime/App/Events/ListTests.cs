namespace PLang.Tests.App.Events;

// An actor's events and the lists they fill: a goal's, a step's and a catalog action's list loads on its first ask with
// every event whose Item has it, in the order bound (pull); an event bound later joins every loaded list it runs for
// (push). The thing an event is bound on answers which items it runs for: itself, each item of a type, each action of a
// module, each goal a pattern names. Inside one of plang's own goals no event runs, unless event.setting.goal is all.
public class ListTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private global::app.goal.@this Goal(string path)
    {
        var goal = Make.Goal(Ctx, System.IO.Path.GetFileNameWithoutExtension(path), path);
        _app.goal.list.Add(goal);
        return goal;
    }

    // The user's event on what's moment `start`, recording name when it runs.
    private async Task<global::app.@event.@this> Record(global::app.type.item.@this what, global::app.@event.When when, List<string> ran, string name)
        => await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(what, "start", when,
            (_, _, ctx) => { lock (ran) ran.Add(name); return Task.FromResult(ctx.Ok()); }, _app.actor.list.User,
            global::app.@event.code.@this.Always));

    [Test] public async Task EachGoal_AndOneGoal_BothRunForIt_InTheOrderBound_BeforeAndAfter()
    {
        var process = Goal("/Process.goal");
        var ran = new List<string>();
        await Record(_app.type.list["goal"], global::app.@event.When.before, ran, "each before");
        await Record(process, global::app.@event.When.before, ran, "process before");
        await Record(_app.type.list["goal"], global::app.@event.When.after, ran, "each after");
        await Record(process, global::app.@event.When.after, ran, "process after");

        await (await process.Start(Ctx)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[] { "each before", "process before", "each after", "process after" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task AGoalRunAsTheSystem_DoesNotRunTheUsersEvents()
    {
        var process = Goal("/Process.goal");
        var ran = new List<string>();
        await Record(_app.type.list["goal"], global::app.@event.When.before, ran, "user's");

        await (await process.Start(_app.actor.list.System.Context)).IsSuccess();
        await Assert.That(ran).IsEmpty();

        await (await process.Start(Ctx)).IsSuccess();
        await Assert.That(ran).IsEquivalentTo(new[] { "user's" });
    }

    [Test] public async Task AnEventBoundAfterAGoalsFirstRun_JoinsItsList_AndRunsAtItsNextStart()
    {
        var process = Goal("/Process.goal");
        var ran = new List<string>();
        await (await process.Start(Ctx)).IsSuccess();
        var loaded = (await _app.actor.list.User.@event.On(process));

        await Record(_app.type.list["goal"], global::app.@event.When.before, ran, "late");
        await (await process.Start(Ctx)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[] { "late" });
        await Assert.That((await _app.actor.list.User.@event.On(process))).IsSameReferenceAs(loaded);
    }

    // the goal's own event unbound, its list is made again from what applies: its "each goal" event still runs
    [Test] public async Task UnbindingAGoalsOwnEvent_LeavesItsEachGoalEventsRunning()
    {
        var process = Goal("/Process.goal");
        var ran = new List<string>();
        await Record(_app.type.list["goal"], global::app.@event.When.before, ran, "each");
        var own = await Record(process, global::app.@event.When.before, ran, "own");
        await (await process.Start(Ctx)).IsSuccess();

        own.Remove();
        await (await process.Start(Ctx)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[] { "each", "own", "each" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // a list still holding events stays when one is unbound: a goal already running sees, at its after, an event bound
    // during its run
    [Test] public async Task AnEventBoundDuringARun_AfterAnUnbind_RunsAtThatRunsAfter()
    {
        var process = Goal("/Process.goal");
        var ran = new List<string>();
        var own = await Record(process, global::app.@event.When.before, ran, "own");
        var user = _app.actor.list.User;
        await user.@event.Add(new global::app.@event.code.@this(_app.type.list["goal"], "start", global::app.@event.When.before,
            async (_, _, ctx) =>
            {
                own.Remove();
                await Record(_app.type.list["goal"], global::app.@event.When.after, ran, "late");
                return ctx.Ok();
            }, user, global::app.@event.code.@this.Always));

        await (await process.Start(Ctx)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[] { "own", "late" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task APattern_RunsForEachGoalItNames_LoadedBeforeOrAfter_AndNoOther()
    {
        var orders = Goal("/api/Orders.goal");
        var process = Goal("/Process.goal");
        var ran = new List<string>();
        var pattern = await (await _app.goal.list.Find("/api/*")).Value();
        await Assert.That(pattern).IsTypeOf<global::app.goal.pattern.@this>();
        await Record(pattern!, global::app.@event.When.before, ran, "api");
        var later = Goal("/api/Users.goal");

        await (await orders.Start(Ctx)).IsSuccess();
        await (await process.Start(Ctx)).IsSuccess();
        await (await later.Start(Ctx)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[] { "api", "api" });
        await Assert.That((await pattern!.Start(Ctx)).Error?.Key).IsEqualTo("NotAGoal");
    }

    // a glob is rooted as a plang path is: `api/*` in the folder of the goal whose step names it, `/api/*` from the root;
    // a goal loaded later under it is named too
    [Test] public async Task APattern_IsRootedAsAPathIs_InItsGoalsFolder_OrFromTheRoot()
    {
        var shop = Goal("/shop/Bind.goal");
        var near = Goal("/shop/api/Orders.goal");
        var root = Goal("/api/Orders.goal");
        var ran = new List<string>();
        await Record((await (await _app.goal.list.Find("api/*", shop)).Value())!, global::app.@event.When.before, ran, "api/*");
        await Record((await (await _app.goal.list.Find("/api/*", shop)).Value())!, global::app.@event.When.before, ran, "/api/*");
        var later = Goal("/shop/api/Users.goal");

        await (await near.Start(Ctx)).IsSuccess();
        await (await root.Start(Ctx)).IsSuccess();
        await (await later.Start(Ctx)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[] { "api/*", "/api/*", "api/*" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task AModulesEvent_RunsForEachOfItsActions_AndNoOtherModules()
    {
        var events = _app.actor.list.User.@event;
        await Record(_app.Module("http"), global::app.@event.When.before, new List<string>(), "http");

        await Assert.That((await events.On(_app.Module("http")["request"]!)).before.Count).IsEqualTo(1);
        await Assert.That((await events.On(_app.Module("http")["download"]!)).before.Count).IsEqualTo(1);
        await Assert.That((await events.On(_app.Module("file")["read"]!)).before.Count).IsEqualTo(0);
    }

    // an actor's events are reached only as the asker's (%!app.event%): a path through another actor reaches nothing
    [Test] public async Task AnotherActorsEvents_AreNotReachedByPath()
    {
        await Record(_app.type.list["goal"], global::app.@event.When.before, new List<string>(), "x");

        var read = await new global::app.type.item.variable.parser.@this("%!app.actor.user.event%", _app.type.list).Variable.Single()
            .Start(_app.actor.list.System.Context);

        await Assert.That(read.Success && read.IsInitialized).IsFalse();
    }

    [Test] public async Task AppEvent_ListsTheRunningActorsEvents()
    {
        var bound = await Record(_app.type.list["goal"], global::app.@event.When.before, new List<string>(), "x");
        var read = await new global::app.type.item.variable.parser.@this("%!app.event.list%", _app.type.list).Variable.Single().Start(Ctx);
        var system = await new global::app.type.item.variable.parser.@this("%!app.event.list%", _app.type.list).Variable.Single()
            .Start(_app.actor.list.System.Context);

        // the system events (the os's Hello, taken in from the test parent) aside: what a program bound
        bool Bound(global::app.data.@this row) => row.Peek() is global::app.@event.@this @event && @event.action?.Step?.Goal.IsSystem != true;
        var events = (await read.Value() as global::app.type.item.list.@this<global::app.@event.@this>)!.Items(Ctx).Where(Bound).ToList();
        await Assert.That(events.Count).IsEqualTo(1);
        await Assert.That(events[0].Peek()).IsSameReferenceAs(bound);
        await Assert.That((await system.Value() as global::app.type.item.list.@this<global::app.@event.@this>)!.Items(Ctx).Where(Bound)).IsEmpty();
    }

    // Under event.setting.goal app (the default) a run inside one of plang's own goals runs no events: reading %!error%
    // a thousand times (each a system goal) runs Log once, for the user's goal; under all, every system goal's start
    // runs it too.
    [Test] public async Task InsidePlangsOwnGoals_EventsRunOnlyUnderAll()
    {
        var run = Goal("/Run.goal");
        var system = Goal("/system/error/Show.goal");
        var ran = new List<string>();
        await Record(_app.type.list["goal"], global::app.@event.When.before, ran, "log");

        await (await run.Start(Ctx)).IsSuccess();
        for (var i = 0; i < 1000; i++) await (await system.Start(Ctx)).IsSuccess();
        await Assert.That(ran.Count).IsEqualTo(1);

        Ctx.Actor!.Setting.Set(Ctx.Actor!.Setting.Address(typeof(global::app.@event.setting.@this), "goal"),
            (global::app.type.item.choice.@this<global::app.@event.setting.Goal>)global::app.@event.setting.Goal.all);
        ran.Clear();
        await (await run.Start(Ctx)).IsSuccess();
        for (var i = 0; i < 1000; i++) await (await system.Start(Ctx)).IsSuccess();
        await Assert.That(ran.Count).IsEqualTo(1001);
    }

    // An action's own clauses run inside plang's goals whatever the setting: they are the program's, not an actor's events.
    [Test] public async Task AnActionsOwnClauses_RunInsidePlangsOwnGoals()
    {
        var show = Make.Goal(Ctx, "Show", "/system/Show.goal", Make.Step("read a file that isn't there",
            Make.With(Make.Action(Ctx, "file", "read", ("Path", "missing.txt")), Make.Action(Ctx, "on", "error", ("ignore", true)))));
        _app.goal.list.Add(show);

        await (await show.Start(Ctx)).IsSuccess();
    }
}
