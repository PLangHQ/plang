using app.actor.context;

using app.error;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.settings;

public class SettingsDataTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_settings_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    // The setting as the actor's settings hold it now — llm's own class.
    private global::app.module.llm.setting.@this Llm(global::app.actor.context.@this ctx)
        => ctx.Setting.Of<global::app.module.llm.setting.@this>();

    private static global::app.data.@this<global::app.type.item.setting.@this> Given(
        global::app.type.item.setting.@this setting, global::app.actor.context.@this ctx)
        => new("Setting", setting, context: ctx);

    [Test]
    public async Task Save_StoresTheSettingWhole_AsTheActorsRow()
    {
        var ctx = _app.actor.list.System.Context;
        var llm = Llm(ctx);
        llm.Cache = false;

        var result = await new global::app.module.setting.Save(ctx) { Setting = Given(llm, ctx) }.Start();
        await result.IsSuccess();
        // no value rides out — a setting may hold secrets
        await Assert.That(result.Peek() is null or global::app.type.item.@null.@this).IsTrue();
        await Assert.That(Llm(ctx).Cache == false).IsTrue();
    }

    [Test]
    public async Task Remove_GoesBackToTheDefaults()
    {
        var ctx = _app.actor.list.System.Context;
        var llm = Llm(ctx);
        llm.Cache = false;
        await (await new global::app.module.setting.Save(ctx) { Setting = Given(llm, ctx) }.Start()).IsSuccess();

        var result = await new global::app.module.setting.Remove(ctx) { Setting = Given(Llm(ctx), ctx) }.Start();
        await result.IsSuccess();
        await Assert.That(Llm(ctx).Cache == true).IsTrue();
    }

    [Test]
    public async Task Save_ANodeThatIsNoClass_IsRefused()
    {
        var ctx = _app.actor.list.System.Context;
        var node = new global::app.type.item.setting.module.@this("http");

        var result = await new global::app.module.setting.Save(ctx) { Setting = Given(node, ctx) }.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotASettingClass");
    }

    [Test]
    public async Task ActorDataSource_IsCreatedLazily()
    {
        // Accessing DataSource should create the .db directory
        var ds = _app.store;
        await Assert.That(ds).IsNotNull();

        var dbDir = System.IO.Path.Combine(_tempDir, ".db");
        await Assert.That(System.IO.Directory.Exists(dbDir)).IsTrue();
    }

    // --- Store-level error path ---

    [Test]
    public async Task Settings_CorruptDatabase_ReturnsSettingsError()
    {
        // Trigger DataSource creation so the DB file exists
        _ = _app.store;

        // Corrupt the database file — overwrite with garbage
        var dbPath = System.IO.Path.Combine(_tempDir, ".db", "system.sqlite");
        System.IO.File.WriteAllText(dbPath, "NOT A VALID SQLITE DATABASE FILE");

        // The store surfaces a SettingsError, not a throw.
        var resolved = await _app.store.Get<global::app.type.item.@this>("settings", "AnyKey");
        await resolved.IsFailure();
        await Assert.That(resolved.Error is SettingsError).IsTrue();
    }
}
