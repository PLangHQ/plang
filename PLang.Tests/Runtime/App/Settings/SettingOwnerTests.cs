namespace PLang.Tests.App.Settings;

/// <summary>
/// The owners of the CLI flags read their settings: this run's values from the flag, the saved rows once
/// the app has loaded them. Debug and each call stack hold theirs (every step reads them) and are built
/// again when a value under their path is written.
/// </summary>
public class SettingOwnerTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    // A plang `set` of a call stack option, through its owner (a call stack is an actor's), reaches the
    // stack that reads it on every push.
    [Test] public async Task CallStack_TakesAPlangSet()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That(app.actor.list.User.CallStack.Timing.Value).IsFalse();

        var set = await new global::app.type.item.variable.parser.@this("%!app.actor[\"user\"].callstack.setting.timing%").Variable.Single()
            .Set(new global::app.data.@this("timing", true, context: ctx), ctx);
        await set.IsSuccess();

        await Assert.That(app.actor.list.User.CallStack.Timing.Value).IsTrue();
        await Assert.That(app.actor.list.System.CallStack.Timing.Value).IsFalse();
    }

    // The system's value (a CLI flag) reaches both actors' stacks — the user falls back to it.
    [Test] public async Task CallStack_TakesTheSystemsValue()
    {
        await using var app = new global::app.@this("/test").Testing();
        await app.actor.list.System.Setting.Set("app.callstack.setting", new Dictionary<string, object?> { ["history"] = true }).IsSuccess();
        await Assert.That(app.actor.list.System.CallStack.History.Value).IsTrue();
        await Assert.That(app.actor.list.User.CallStack.History.Value).IsTrue();
    }

    // A call stack reads through its actor's context's settings: a value written there (a goal's own set) reaches
    // that actor's stack, never another's.
    [Test] public async Task CallStack_TakesItsContextsValue_AndNoOthers()
    {
        await using var app = new global::app.@this("/test").Testing();
        await app.actor.list.User.Context.Setting.Set("app.callstack.setting", new Dictionary<string, object?> { ["timing"] = true }).IsSuccess();
        await Assert.That(app.actor.list.User.CallStack.Timing.Value).IsTrue();
        await Assert.That(app.actor.list.System.CallStack.Timing.Value).IsFalse();
    }

    // Debug, once born, takes a later value under its path.
    [Test] public async Task Debug_TakesALaterValue()
    {
        await using var app = new global::app.@this("/test").Testing();
        app.Debug = new global::app.module.debug.@this(app.actor.list.System.Context);
        await Assert.That(app.Debug.Setting.Length.Max.ToInt32()).IsEqualTo(500);

        await app.actor.list.System.Setting.Set("debug.setting", new Dictionary<string, object?> { ["length"] = new Dictionary<string, object?> { ["max"] = 10 } }).IsSuccess();
        await Assert.That(app.Debug.Setting.Length.Max.ToInt32()).IsEqualTo(10);
    }

    // A value the option can't take is refused at the flag, and nothing is written.
    [Test] public async Task Flag_RefusesAValueItsOptionCantTake()
    {
        await using var app = new global::app.@this("/test").Testing();
        var set = app.actor.list.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["format"] = "nonsense" });
        await set.IsFailure();
        await Assert.That(app.actor.list.System.Context.Setting.Of<global::app.test.setting.@this>().Format.Value.ToString())
            .IsEqualTo("json");
    }

    // The app's own setting, read through the app: %!app.setting.create%.
    [Test] public async Task AppSetting_ReadsThroughTheApp()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That((await (await Read("%!app.setting.create%", app.actor.list.User.Context)).Value())?.ToString()).IsEqualTo("false");

        await app.actor.list.System.Setting.Set("app.setting", new Dictionary<string, object?> { ["create"] = true }).IsSuccess();
        await Assert.That((await (await Read("%!app.setting.create%", app.actor.list.User.Context)).Value())?.ToString()).IsEqualTo("true");
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
                var saved = new global::app.test.setting.@this { Timeout = System.TimeSpan.FromSeconds(7) };
                await (await first.actor.list.System.Setting.Save(saved)).IsSuccess();
            }

            await using var app = new global::app.@this(dir);
            await app.Load();
            await Assert.That(app.actor.list.User.Context.Setting.Of<global::app.test.setting.@this>().Timeout.Value.TotalSeconds).IsEqualTo(7);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { } }
    }
}
