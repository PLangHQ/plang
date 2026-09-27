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
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        await Assert.That(app.User.CallStack.Timing.Value).IsFalse();

        var set = await new global::app.type.item.variable.parser.@this("%!app.user.callstack.setting.timing%").Variable.Single()
            .Set(new global::app.data.@this("timing", true, context: ctx), ctx);
        await set.IsSuccess();

        await Assert.That(app.User.CallStack.Timing.Value).IsTrue();
        await Assert.That(app.System.CallStack.Timing.Value).IsFalse();
    }

    // The system's value (a CLI flag) reaches both actors' stacks — the user falls back to it.
    [Test] public async Task CallStack_TakesTheSystemsValue()
    {
        await using var app = TestApp.Create("/test");
        await app.System.Setting.Set("app.callstack.setting", new Dictionary<string, object?> { ["history"] = true }).IsSuccess();
        await Assert.That(app.System.CallStack.History.Value).IsTrue();
        await Assert.That(app.User.CallStack.History.Value).IsTrue();
    }

    // Debug, once born, takes a later value under its path.
    [Test] public async Task Debug_TakesALaterValue()
    {
        await using var app = TestApp.Create("/test");
        app.Debug = new global::app.module.action.debug.@this(app.System.Context);
        await Assert.That(app.Debug.MaxLength).IsEqualTo(500);

        await app.System.Setting.Set("debug", new Dictionary<string, object?> { ["maxLength"] = 10 }).IsSuccess();
        await Assert.That(app.Debug.MaxLength).IsEqualTo(10);
    }

    // A value the option can't take is refused at the flag, and nothing is written.
    [Test] public async Task Flag_RefusesAValueItsOptionCantTake()
    {
        await using var app = TestApp.Create("/test");
        var set = app.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["format"] = "nonsense" });
        await set.IsFailure();
        await Assert.That(app.System.Context.Setting.Of<global::app.test.setting.@this>().Format.Clr<global::app.test.Format>())
            .IsEqualTo(global::app.test.Format.Json);
    }

    // The app's own setting, read through the app: %!app.setting.create%.
    [Test] public async Task AppSetting_ReadsThroughTheApp()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That((await (await Read("%!app.setting.create%", app.User.Context)).Value())?.ToString()).IsEqualTo("false");

        await app.System.Setting.Set("app.setting", new Dictionary<string, object?> { ["create"] = true }).IsSuccess();
        await Assert.That((await (await Read("%!app.setting.create%", app.User.Context)).Value())?.ToString()).IsEqualTo("true");
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
                var saved = new global::app.test.setting.@this { TimeoutSeconds = 7 };
                await (await first.System.Setting.Save("app.test.setting", new global::app.data.@this("s", saved, context: first.System.Context))).IsSuccess();
            }

            await using var app = new global::app.@this(dir);
            await app.Load();
            await Assert.That(app.User.Context.Setting.Of<global::app.test.setting.@this>().TimeoutSeconds.ToInt32()).IsEqualTo(7);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { } }
    }
}
