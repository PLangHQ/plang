namespace PLang.Tests.App.Events;

// System events: each file of an app's system/event/ is run as each actor when its events are first asked for
// (event.setting.system load, the default), binding what it binds as that actor's own. The tests write an app's folder:
// a goal made in memory, its .pr written beside it as a build would.
public class SystemEventsTests
{
    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sysevents-" + Guid.NewGuid().ToString("N")[..8]);

    // system/event/Hello.goal in `app`'s folder `root`: `when` `on` (an event path), it calls Say, which sets %said% to
    // `word`.
    private static async Task Hello(global::app.@this app, string root, string word, string on = "%!app.on.start%", string when = "before")
    {
        var context = app.actor.list.User.Context;
        var hello = Make.Goal(context, "Hello", "/system/event/Hello.goal",
            Make.Step($"{when} {on}, call Say", Make.Action(context, "on", "event", ("Event", on),
                ("When", when), ("Action", Make.Call(context, "Say")))));
        hello.Child.Add(Make.Goal(context, "Say", "/system/event/Hello.goal",
            Make.Step($"set %said% = {word}", Make.Action(context, "variable", "set", Make.Param(context, "Name", "said", "variable"), ("Value", word)))));
        var source = System.IO.Path.Combine(root, "system/event/Hello.goal");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
        await System.IO.File.WriteAllTextAsync(source, $"Hello\n- {when} {on}, call Say\n");
        var pr = System.IO.Path.Combine(root, hello.Pr!.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        await System.IO.File.WriteAllTextAsync(pr, await context.Pr(hello));
    }

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

    [Test] public async Task UnderSkip_NoSystemEventLoads()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;
        user.Setting.Set(user.Setting.Address(typeof(global::app.@event.setting.@this), "system"),
            (global::app.type.item.choice.@this<global::app.@event.setting.System>)global::app.@event.setting.System.skip);

        await Start(app);

        await Assert.That((await app.@event(user.Context)).Items()).IsEmpty();
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
