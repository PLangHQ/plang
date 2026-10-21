namespace PLang.Tests.App.Settings;

/// <summary>
/// The owners of the CLI flags read their settings: this run's values from the flag, the saved rows once
/// the app has loaded them. Debug and each call stack hold theirs (every step reads them) and are built
/// again when a value under their path is written.
/// </summary>
public class SettingOwnerTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text, ctx.App.type.list).Variable.Single().Start(ctx);

    // A plang `set` of a call stack option, through its owner (a call stack is an actor's), reaches the
    // stack that reads it on every push.
    [Test] public async Task CallStack_TakesAPlangSet()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That((await app.actor.list.User.Context.call.Timing()).Value).IsFalse();

        var set = await new global::app.type.item.variable.parser.@this("%!app.actor[\"user\"].context.call.setting.timing%", ctx.App.type.list).Variable.Single()
            .Set(new global::app.data.@this("timing", true, context: ctx), ctx);
        await set.IsSuccess();

        await Assert.That((await app.actor.list.User.Context.call.Timing()).Value).IsTrue();
        await Assert.That((await app.actor.list.System.Context.call.Timing()).Value).IsFalse();
    }

    // The system's value (a CLI flag) reaches both actors' stacks — the user falls back to it.
    [Test] public async Task CallStack_TakesTheSystemsValue()
    {
        await using var app = new global::app.@this("/test").Testing();
        await (await app.actor.list.System.Setting.Flag("app.call.setting", new Dictionary<string, object?> { ["history"] = true }, app.actor.list.System.Context)).IsSuccess();
        await Assert.That((await app.actor.list.System.Context.call.History()).Value).IsTrue();
        await Assert.That((await app.actor.list.User.Context.call.History()).Value).IsTrue();
    }

    // A call stack reads through its actor's context's settings: a value written there (a goal's own set) reaches
    // that actor's stack, never another's.
    [Test] public async Task CallStack_TakesItsContextsValue_AndNoOthers()
    {
        await using var app = new global::app.@this("/test").Testing();
        await (await app.actor.list.User.Context.Setting.Flag("app.call.setting", new Dictionary<string, object?> { ["timing"] = true }, app.actor.list.User.Context)).IsSuccess();
        await Assert.That((await app.actor.list.User.Context.call.Timing()).Value).IsTrue();
        await Assert.That((await app.actor.list.System.Context.call.Timing()).Value).IsFalse();
    }

    // Debug, once born, takes a later value under its path.
    [Test] public async Task Debug_TakesALaterValue()
    {
        await using var app = new global::app.@this("/test").Testing();
        app.Debug = new global::app.module.debug.@this(app.actor.list.System.Context);
        await Assert.That((await app.Debug.Setting()).Length.Max.ToInt32()).IsEqualTo(500);

        await (await app.actor.list.System.Setting.Flag("debug.setting", new Dictionary<string, object?> { ["length"] = new Dictionary<string, object?> { ["max"] = 10 } }, app.actor.list.System.Context)).IsSuccess();
        await Assert.That((await app.Debug.Setting()).Length.Max.ToInt32()).IsEqualTo(10);
    }

    // A value the option can't take is refused at the flag, and nothing is written.
    [Test] public async Task Flag_RefusesAValueItsOptionCantTake()
    {
        await using var app = new global::app.@this("/test").Testing();
        var set = await app.actor.list.System.Setting.Flag("app.test.setting", new Dictionary<string, object?> { ["format"] = "nonsense" }, app.actor.list.System.Context);
        await set.IsFailure();
        await Assert.That((await app.actor.list.System.Context.Setting.Get<global::app.test.setting.@this>()).Format.Value.ToString())
            .IsEqualTo("json");
    }

    // The app's own setting, read through the app: %!app.setting.create%.
    [Test] public async Task AppSetting_ReadsThroughTheApp()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That((await (await Read("%!app.setting.create%", app.actor.list.User.Context)).Value())?.ToString()).IsEqualTo("false");

        await (await app.actor.list.System.Setting.Flag("app.setting", new Dictionary<string, object?> { ["create"] = true }, app.actor.list.System.Context)).IsSuccess();
        await Assert.That((await (await Read("%!app.setting.create%", app.actor.list.User.Context)).Value())?.ToString()).IsEqualTo("true");
    }

    // A setting always has a value — its class default, or the one written: the CLI's --build.setting={"cache":"skip"} reads
    // as skip in a goal, and a `set default` on it leaves the CLI's value.
    [Test] public async Task ASettingTheCliWrote_ReadsAsWritten_AndASetDefaultLeavesIt()
    {
        await using var app = new global::app.@this("/test").Testing();
        // as the CLI does it: build mode born, then the flag's values written
        app.Build = new global::app.module.build.@this(app.actor.list.System.Context);
        var path = new global::app.module.build.setting.@this(new global::app.type.@this(typeof(global::app.module.build.setting.@this))).Path;
        await (await app.actor.list.System.Setting.Flag(path, new Dictionary<string, object?> { ["cache"] = "skip" }, app.actor.list.System.Context)).IsSuccess();

        foreach (var ctx in new[] { app.actor.list.System.Context, app.actor.list.User.Context })
        {
            var read = await Read("%!build.setting.cache%", ctx);
            await Assert.That(read.IsInitialized).IsTrue();
            await Assert.That((await read.Value())?.ToString()).IsEqualTo("skip");
        }

        var system = app.actor.list.System.Context;
        await (await global::PLang.Tests.Shared.Make.Action(system, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(system, "Name", "%!build.setting.cache%", "variable"),
            ("Value", "use"), ("Default", true)).Start(system)).IsSuccess();
        await Assert.That((await (await Read("%!build.setting.cache%", system)).Value())?.ToString()).IsEqualTo("skip");
    }

    // The saved rows are read when the app loads; after it the in-memory door has them.
    [Test] public async Task Rows_AreThereAfterTheAppLoads()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_owner_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            await using (var first = new global::app.@this(dir))
            {
                var saved = new global::app.test.setting.@this(new global::app.type.@this(typeof(global::app.test.setting.@this))) { Timeout = System.TimeSpan.FromSeconds(7) };
                await (await first.actor.list.System.Setting.Save(saved)).IsSuccess();
            }

            await using var app = new global::app.@this(dir);
            await app.Load();
            await Assert.That((await app.actor.list.User.Context.Setting.Get<global::app.test.setting.@this>()).Timeout.Value.TotalSeconds).IsEqualTo(7);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { } }
    }
}
