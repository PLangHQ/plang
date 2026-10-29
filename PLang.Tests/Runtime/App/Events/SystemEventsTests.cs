namespace PLang.Tests.App.Events;

// System events: each file of an app's system/event/ is run as each actor when the app starts (event.setting.system
// load, the default), binding what it binds as that actor's own. The tests write an app's folder: a goal made in memory,
// its .pr written beside it as a build would.
public class SystemEventsTests
{
    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sysevents-" + Guid.NewGuid().ToString("N")[..8]);

    // system/event/Hello.goal in `root`: before the app starts it calls Say, which sets %said% to `word`.
    private static async Task Hello(global::app.@this app, string root, string word)
    {
        var context = app.actor.list.User.Context;
        var hello = Make.Goal(context, "Hello", "/system/event/Hello.goal",
            Make.Step("before the app starts, call Say", Make.Action(context, "on", "event", ("Event", "%!app.on.start%"),
                ("When", "before"), ("Action", Make.Call(context, "Say")))));
        hello.Child.Add(Make.Goal(context, "Say", "/system/event/Hello.goal",
            Make.Step($"set %said% = {word}", Make.Action(context, "variable", "set", Make.Param(context, "Name", "said", "variable"), ("Value", word)))));
        foreach (var (file, text) in new[] { ("system/event/Hello.goal", "Hello\n- before the app starts, call Say\n") })
        {
            var source = System.IO.Path.Combine(root, file);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
            await System.IO.File.WriteAllTextAsync(source, text);
        }
        var pr = System.IO.Path.Combine(root, hello.Pr!.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        await System.IO.File.WriteAllTextAsync(pr, await context.Pr(hello));
    }

    [Test] public async Task TheAppsSystemEvent_RunsAtItsStart()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;

        await (await user.@event.Load(user.Context)).IsSuccess();
        await user.@event[app].before.Start(app, user.Context);

        await Assert.That(await user.Context.Variable.GetValue("said")).IsEqualTo("hello");
    }

    [Test] public async Task UnderSkip_NoSystemEventLoads()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await Hello(app, root, "hello");
        var user = app.actor.list.User;
        user.Setting.Set(user.Setting.Address(typeof(global::app.@event.setting.@this), "system"),
            (global::app.type.item.choice.@this<global::app.@event.setting.System>)global::app.@event.setting.System.skip);

        await (await user.@event.Load(user.Context)).IsSuccess();
        await user.@event[app].before.Start(app, user.Context);

        await Assert.That(user.@event.Items()).IsEmpty();
        await Assert.That((await user.Context.Variable.Get("said")).IsInitialized).IsFalse();
    }
}
