namespace PLang.Tests.App.Events;

// System events: each file of an app's system/event/ is run as each actor when its events are first asked for
// (event.setting.system load, the default), binding what it binds as that actor's own. The tests write an app's folder:
// a goal made in memory, its .pr written beside it as a build would.
public class SystemEventsTests
{
    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sysevents-" + Guid.NewGuid().ToString("N")[..8]);

    // system/event/Hello.goal in `app`'s folder `root`: `when` `on` (an event path), it calls Say, which sets %said% to
    // `word`. Built in the shared test parent, so building it is no ask of the app's events.
    private static async Task Hello(global::app.@this app, string root, string word, string on = "%!app.on.start%", string when = "before",
        string at = "/system/event/Hello.goal")
    {
        var context = global::PLang.Tests.Parent.Actor.Context;
        var name = System.IO.Path.GetFileNameWithoutExtension(at);
        var hello = Make.Goal(context, name, at,
            Make.Step($"{when} {on}, call Say", Make.Action(context, "on", "event", ("Event", on),
                ("When", when), ("Action", Make.Call(context, "Say")))));
        hello.Child.Add(Make.Goal(context, "Say", at,
            Make.Step($"set %said% = {word}", Make.Action(context, "variable", "set", Make.Param(context, "Name", "said", "variable"), ("Value", word)))));
        var source = System.IO.Path.Combine(root, at.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
        await System.IO.File.WriteAllTextAsync(source, $"{name}\n- {when} {on}, call Say\n");
        var pr = System.IO.Path.Combine(root, hello.Pr!.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        await System.IO.File.WriteAllTextAsync(pr, await context.Pr(hello));
    }

    // system/event/Mark.goal in `root`, built in the shared test parent (so building it loads none of the apps tested):
    // it binds an event, so an actor whose load ran it holds one bound there (Marked). A load runs in its own context,
    // so what a file sets stays in it; what it binds is the actor's.
    private static Task Mark(string root) => Hello(null!, root, "marked", at: "/system/event/Mark.goal");

    // whether actor's own load ran Mark: an event of its own bound there (one taken in from its maker is the maker's run)
    private static bool Marked(global::app.actor.@this actor)
        => actor.@event.Items().OfType<global::app.@event.call.@this>().Any(@event => @event.IsBoundIn("/system/event/Mark"));

    // the app's start, run before for its user
    private static async Task Start(global::app.@this app)
    {
        var user = app.actor.list.User;
        await (await user.@event.On(app)).before.Start(app, user.Context);
    }

    // the system event's goals ask the events while they load: the load lets its own flow through, so the first ask ends
    [Test] public async Task TheAppsSystemEvent_RunsAtItsStart()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");

        await Start(app).WaitAsync(TimeSpan.FromSeconds(20));

        await Assert.That(await app.actor.list.User.Context.Variable.GetValue("said")).IsEqualTo("hello");
    }

    // the os's own Hello (os/system/event/Hello.goal, its built .pr), loaded by a root app from the os's system/event/:
    // at the app's start it writes out that it ran
    [Test] public async Task TheOsHello_WritesOutAtARootAppsStart()
    {
        await using var app = new global::app.@this(Root()).Testing();
        var output = new global::PLang.Tests.Shared.CapturedOutput(app);

        await Start(app);

        await Assert.That(output.Text).Contains("This is system event");
    }

    [Test] public async Task UnderSkip_NoSystemEventLoads()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;
        user.Setting.Set(user.Setting.Address(typeof(global::app.@event.setting.@this), "system"),
            (global::app.type.item.choice.@this<global::app.@event.setting.System>)global::app.@event.setting.System.skip);

        await Start(app);

        // skip leaves the app's own files unrun: no call event of its own (its maker's, its system events among them,
        // still come in as inherited)
        await Assert.That((await app.@event(user.Context)).Items().OfType<global::app.@event.call.@this>()).IsEmpty();
        await Assert.That((await user.Context.Variable.Get("said")).IsInitialized).IsFalse();
    }

    [Test] public async Task AFirstBind_JoinsAfterTheSystemEvents()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;

        var mine = await user.@event.Add(new global::app.@event.code.@this(app.type.list["goal"], "start", global::app.@event.When.before,
            (_, result, _) => Task.FromResult(result), user, global::app.@event.code.@this.Always));

        var events = (await app.@event(user.Context)).Items().ToList();
        await Assert.That(events.Count).IsEqualTo(2);
        await Assert.That(events[0]).IsTypeOf<global::app.@event.call.@this>();
        await Assert.That(events[1]).IsSameReferenceAs(mine);
    }

    [Test] public async Task ASubAppsOwnSystemEvent_ReplacesTheOneItsMakerBoundFromTheFileOfThatName()
    {
        var root = Root();
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(maker, root, "maker");
        await Start(maker);
        await Assert.That(await maker.actor.list.User.Context.Variable.GetValue("said")).IsEqualTo("maker");

        var subRoot = System.IO.Path.Combine(root, "sub");
        await using var sub = new global::app.@this(subRoot, maker.actor.list.User);
        await Hello(sub, subRoot, "sub");
        await Start(sub);

        var user = sub.actor.list.User;
        await Assert.That(await user.Context.Variable.GetValue("said")).IsEqualTo("sub");
        await Assert.That((await sub.@event(user.Context)).Items().OfType<global::app.@event.inherited.@this>()).IsEmpty();
    }

    // a sub app's own system/event/Start.goal replaces what its maker bound in the goal at /system/event/Start, never what
    // it bound in a goal of the same name elsewhere (its /Start.goal)
    [Test] public async Task ASubAppsOwnSystemEvent_ReplacesOnlyTheMakersEventFromTheSameAddress()
    {
        var root = Root();
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(maker, root, "maker's system event", at: "/system/event/Start.goal");
        await Hello(maker, root, "maker's start", "%!app.type.goal.on.start%", at: "/Start.goal");
        var makers = maker.actor.list.User.Context;
        await (await (await (await maker.goal.list.Find("/Start")).Value())!.Start(makers)).IsSuccess();

        var subRoot = System.IO.Path.Combine(root, "sub");
        await using var sub = new global::app.@this(subRoot, maker.actor.list.User);
        await Hello(sub, subRoot, "sub", "%!app.on.start%", at: "/system/event/Start.goal");
        var user = sub.actor.list.User;
        // where each event taken in from the maker was bound (the os's own system events come down too)
        var taken = (await sub.@event(user.Context)).Items().OfType<global::app.@event.inherited.@this>()
            .Select(@event => @event.action?.Step?.Goal.Address).ToList();

        await Assert.That(taken).Contains("/Start");
        await Assert.That(taken).DoesNotContain("/system/event/Start");
    }

    // a sub app's System, loading, takes in its maker's System's events: the maker's System loads, its User doesn't
    [Test] public async Task ASubAppsLoad_LoadsOnlyItsMakersActorOfItsRole()
    {
        var root = Root();
        await Mark(root);
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await using var sub = new global::app.@this(System.IO.Path.Combine(root, "sub"), maker.actor.list.User);

        await sub.@event(sub.actor.list.System.Context);

        await Assert.That(Marked(maker.actor.list.System)).IsTrue();
        await Assert.That(Marked(maker.actor.list.User)).IsFalse();
    }

    // a sub app over its maker's folder has no system/event of its own: the folder's files are the maker's, run there,
    // and come to the sub app as the maker's events, taken in — never run a second time
    [Test] public async Task ASubAppOverItsMakersFolder_RunsNoSystemEventFile_AndHasTheMakersEvents()
    {
        var root = Root();
        await Mark(root);
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(maker, root, "maker");
        await Start(maker);
        await using var sub = new global::app.@this(root, maker.actor.list.User);

        var events = (await sub.@event(sub.actor.list.User.Context)).Items().ToList();

        await Assert.That(Marked(sub.actor.list.User)).IsFalse();
        await Assert.That(Marked(sub.actor.list.System)).IsFalse();
        await Assert.That(events.OfType<global::app.@event.inherited.@this>()).IsNotEmpty();
        await Assert.That(events.OfType<global::app.@event.call.@this>()).IsEmpty();
    }

    // the system event's call events the actor holds — one per load of the app's Hello
    private static int Loads(global::app.actor.@this actor)
        => actor.@event.Items().OfType<global::app.@event.call.@this>().Count();

    // The first ask may come from anything that runs. Each below is the first: the load runs once, and the events are
    // there.
    [Test] public async Task AGoalsStart_IsAFirstAsk_TheEventsLoadOnce()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;
        var parent = global::PLang.Tests.Parent.Actor.Context;
        var goal = Make.Goal(parent, "Work", Make.Step("set %done% = yes",
            Make.Action(parent, "variable", "set", Make.Param(parent, "Name", "done", "variable"), ("Value", "yes"))));
        await Assert.That(Loads(user)).IsEqualTo(0);

        await (await goal.Start(user.Context)).IsSuccess();
        await (await goal.Start(user.Context)).IsSuccess();

        await Assert.That(Loads(user)).IsEqualTo(1);
    }

    [Test] public async Task ASubAppsFirstAsk_LoadsOnce_AndTakesInItsMakers()
    {
        var root = Root();
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(maker, root, "maker");
        await using var sub = new global::app.@this(root, maker.actor.list.User);
        var user = sub.actor.list.User;

        await user.Context.Variable.Set("x", 1);
        await user.Context.Variable.Set("x", 2);

        await Assert.That(Loads(maker.actor.list.User)).IsEqualTo(1);
        await Assert.That(user.@event.Items().OfType<global::app.@event.inherited.@this>().Count()).IsEqualTo(1);
    }

    [Test] public async Task ARestore_IsAFirstAsk_TheEventsLoadOnce()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing().TestIdentity();
        await Hello(app, root, "hello");
        var snapshot = await app.Snapshot(app.actor.list.User.Context).Saved();
        await using var fresh = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing().TestIdentity();
        var user = fresh.actor.list.User;

        await fresh.Restore(snapshot, user.Context);
        await fresh.@event(user.Context);

        await Assert.That(Loads(user)).IsEqualTo(1);
    }

    // A load that fails isn't kept: the next ask loads again. The event setting the load reads holds a value no choice
    // names, so that load throws; set right, the next ask loads.
    [Test] public async Task AFailedLoad_IsNotKept_TheNextAskLoads()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;
        var system = user.Setting.Address(typeof(global::app.@event.setting.@this), "system");
        user.Setting.Set(system, "neither");

        await Assert.That(async () => await app.@event(user.Context)).Throws<System.Exception>();
        user.Setting.Set(system, (global::app.type.item.choice.@this<global::app.@event.setting.System>)global::app.@event.setting.System.load);
        await app.@event(user.Context);

        await Assert.That(Loads(user)).IsEqualTo(1);
    }

    // all or nothing: a load that ran its system event and then failed (its maker's load throws) leaves no event, and the
    // next ask binds the system event once
    [Test] public async Task AFailedLoad_DropsWhatItJoined_TheNextBindsItOnce()
    {
        var root = Root();
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var subRoot = System.IO.Path.Combine(root, "sub");
        await using var sub = new global::app.@this(subRoot, maker.actor.list.User);
        await Hello(sub, subRoot, "sub");
        var makers = maker.actor.list.System;
        var system = makers.Setting.Address(typeof(global::app.@event.setting.@this), "system");
        makers.Setting.Set(system, "neither");
        var own = sub.actor.list.System;

        await Assert.That(async () => await sub.@event(own.Context)).Throws<System.Exception>();
        await Assert.That(own.@event.Items()).IsEmpty();
        makers.Setting.Set(system, (global::app.type.item.choice.@this<global::app.@event.setting.System>)global::app.@event.setting.System.load);
        await sub.@event(own.Context);

        await Assert.That(Loads(own)).IsEqualTo(1);
    }

    // system/event/<name>.goal in `root`, written and never built
    private static async Task Unbuilt(string root, string name)
    {
        var source = System.IO.Path.Combine(root, $"system/event/{name}.goal");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
        await System.IO.File.WriteAllTextAsync(source, $"{name}\n- set %loaded% = yes\n");
    }

    private static global::app.goal.@this Work()
    {
        var parent = global::PLang.Tests.Parent.Actor.Context;
        return Make.Goal(parent, "Work", Make.Step("set %done% = yes",
            Make.Action(parent, "variable", "set", Make.Param(parent, "Name", "done", "variable"), ("Value", "yes"))));
    }

    // fail closed: a system event file that doesn't load fails the actor's load with its error, whole — the goal whose
    // start asked answers it, and nothing the load bound stays
    [Test] public async Task AnUnbuiltSystemEventFile_FailsTheLoad_TheGoalAnswersItsError_NothingStays()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        await Unbuilt(root, "Mark");
        var user = app.actor.list.User;

        var started = await Work().Start(user.Context);

        await Assert.That(started.Error?.Key).IsEqualTo("GoalNotBuilt");
        await Assert.That(user.@event.Items()).IsEmpty();
        await Assert.That((await user.Context.Variable.Get("done")).IsInitialized).IsFalse();
    }

    // system/event/Slow.goal in `root`: it sleeps, then binds as Mark does — a load that takes a while
    private static async Task Slow(string root)
    {
        var context = global::PLang.Tests.Parent.Actor.Context;
        var slow = Make.Goal(context, "Slow", "/system/event/Slow.goal",
            Make.Step("wait 500ms", Make.Action(context, "timer", "sleep", ("Duration", "500ms"))));
        var source = System.IO.Path.Combine(root, "system/event/Slow.goal");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
        await System.IO.File.WriteAllTextAsync(source, "Slow\n- wait 500ms\n");
        var pr = System.IO.Path.Combine(root, slow.Pr!.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        await System.IO.File.WriteAllTextAsync(pr, await context.Pr(slow));
    }

    // the load is the actor's: an asker cancelled while it runs stops waiting and answers Cancelled; another asker
    // waiting on the same load gets the events, and the load ran once
    [Test] public async Task AnAskerCancelledWhileTheEventsLoad_StopsWaiting_TheOtherGetsTheEvents()
    {
        var root = Root();
        await Mark(root);
        await Slow(root);
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        // the first asker's flow, cancelled while the load runs: its cancellation pushed on the actor's own context
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        user.Context.PushCancellation(cts);
        global::app.data.@this stopped, ran;
        try
        {
            var first = Work().Start(user.Context);
            using var other = user.Context.Child(user, System.Threading.CancellationToken.None);
            ran = await Work().Start(other);
            stopped = await first;
        }
        finally { user.Context.PopCancellation(); }

        await Assert.That(stopped.Error?.Key).IsEqualTo("Cancelled");
        await ran.IsSuccess();
        await Assert.That(Marked(user)).IsTrue();
        await Assert.That(user.@event.Items().OfType<global::app.@event.call.@this>().Count(@event => @event.IsBoundIn("/system/event/Mark"))).IsEqualTo(1);
    }

    // the file built, the next ask loads it, and the goal runs
    [Test] public async Task AFixedSystemEventFile_LoadsAtTheNextAsk()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        await Unbuilt(root, "Mark");
        var user = app.actor.list.User;
        await Assert.That((await Work().Start(user.Context)).Error?.Key).IsEqualTo("GoalNotBuilt");

        await Mark(root);
        await (await Work().Start(user.Context)).IsSuccess();

        // Hello's event and Mark's, each bound once
        await Assert.That(Loads(user)).IsEqualTo(2);
        await Assert.That(Marked(user)).IsTrue();
        await Assert.That(await user.Context.Variable.GetValue("done")).IsEqualTo("yes");
    }

    // a sub app's own broken file fails the sub app's load; its maker's loads and runs
    [Test] public async Task ASubAppsBrokenSystemEventFile_FailsItsLoad_NotItsMakers()
    {
        var root = Root();
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(maker, root, "maker");
        var subRoot = System.IO.Path.Combine(root, "sub");
        await using var sub = new global::app.@this(subRoot, maker.actor.list.User);
        await Unbuilt(subRoot, "Mark");

        await Assert.That((await Work().Start(sub.actor.list.User.Context)).Error?.Key).IsEqualTo("GoalNotBuilt");
        await (await Work().Start(maker.actor.list.User.Context)).IsSuccess();

        await Assert.That(Loads(maker.actor.list.User)).IsEqualTo(1);
        await Assert.That(sub.actor.list.User.@event.Items()).IsEmpty();
    }

    // a build's Cover asks whether the code's variables name the step's: a path through the app's variables is read hop
    // by hop up to the first variable it reaches, never on into its moments, so asking loads no actor's events
    [Test] public async Task WhetherAPathToAVariablesMoment_NamesAVariable_LoadsNoEvents()
    {
        var root = Root();
        await Mark(root);
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var path = new global::app.type.item.variable.parser.@this("%!app.variable[\"count\"].on.set%", app.type.list).Variable.Single();
        var other = new global::app.type.item.variable.parser.@this("%other%", app.type.list).Variable.Single();
        var count = new global::app.type.item.variable.parser.@this("%count%", app.type.list).Variable.Single();

        await Assert.That(await path.Names(other, user.Context)).IsFalse();
        await Assert.That(await path.Names(count, user.Context)).IsTrue();

        await Assert.That(user.@event.Items()).IsEmpty();
        await Assert.That(Marked(user)).IsFalse();
    }

    [Test] public async Task ACreateEventASystemEventBinds_RunsForAValueTheUserMakes()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "made", "%!app.type.number.on.create%", "after");
        var user = app.actor.list.User;

        await (await app.type.list["number"].Create(5, user.Context, "n")).IsSuccess();

        await Assert.That(await user.Context.Variable.GetValue("said")).IsEqualTo("made");
    }
}
