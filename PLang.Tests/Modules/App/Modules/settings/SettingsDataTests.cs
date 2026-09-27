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

    [Test]
    public async Task SettingsHandler_Get_ExistingKey_ReturnsValue()
    {
        await (await _app.store).Set("settings", "TestKey", new global::app.data.@this("TestKey", "TestValue", context: _app.System.Context));

        var handler = new global::app.module.action.setting.Get(_app.System.Context) { Key = (global::app.type.item.text.@this)"TestKey"
        };

        var result = await handler.Start();
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("TestValue");
    }

    [Test]
    public async Task SettingsHandler_Get_MissingKey_ReturnsAskError()
    {
        var handler = new global::app.module.action.setting.Get(_app.System.Context) { Key = (global::app.type.item.text.@this)"MissingKey"
        };

        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error is AskError).IsTrue();
    }

    [Test]
    public async Task SettingsHandler_Remove_DeletesKey()
    {
        await (await _app.store).Set("settings", "ToRemove", new global::app.data.@this("ToRemove", "value", context: _app.System.Context));

        var handler = new global::app.module.action.setting.Remove(_app.System.Context) { Key = (global::app.type.item.text.@this)"ToRemove"
        };

        var result = await handler.Start();
        await result.IsSuccess();

        // Verify removed
        var getResult = await (await _app.store).Get<global::app.type.item.@this>("settings", "ToRemove");
        await Assert.That(await (await getResult.Value())!.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task ActorDataSource_IsCreatedLazily()
    {
        // Accessing DataSource should create the .db directory
        var ds = await _app.store;
        await Assert.That(ds).IsNotNull();

        var dbDir = System.IO.Path.Combine(_tempDir, ".db");
        await Assert.That(System.IO.Directory.Exists(dbDir)).IsTrue();
    }

    // --- Store-level error path ---

    [Test]
    public async Task Settings_CorruptDatabase_ReturnsSettingsError()
    {
        // Trigger DataSource creation so the DB file exists
        _ = await _app.store;

        // Corrupt the database file — overwrite with garbage
        var dbPath = System.IO.Path.Combine(_tempDir, ".db", "system.sqlite");
        System.IO.File.WriteAllText(dbPath, "NOT A VALID SQLITE DATABASE FILE");

        // The store surfaces a SettingsError, not a throw.
        var resolved = await (await _app.store).Get<global::app.type.item.@this>("settings", "AnyKey");
        await resolved.IsFailure();
        await Assert.That(resolved.Error is SettingsError).IsTrue();
    }
}
