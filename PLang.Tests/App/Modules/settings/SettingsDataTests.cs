using app.actor.context;

using app.errors;
using app.variables;
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
        _app = new PLangEngine(_tempDir);
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
    public async Task Settings_DotNotation_ReturnsStoredValue()
    {
        // Store a setting in the app-level store
        await _app.SettingsStore.Set("settings", "ApiKey", new global::app.data.@this("ApiKey", "sk-test-123"));

        // %Settings.ApiKey% goes through Variables.RegisterNavigable("Settings", ...)
        var resolved = _app.User.Context.Variables.Get("Settings.ApiKey");
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsTrue();
        await Assert.That(resolved.Value?.ToString()).IsEqualTo("sk-test-123");
    }

    [Test]
    public async Task Settings_DotNotation_MissingKey_ReturnsAskError()
    {
        var resolved = _app.User.Context.Variables.Get("Settings.NonExistentKey");
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsFalse();
        await Assert.That(resolved.Error is AskError).IsTrue();

        var askError = (AskError)resolved.Error!;
        await Assert.That(askError.Table).IsEqualTo("settings");
        await Assert.That(askError.DataKey).IsEqualTo("NonExistentKey");
    }

    [Test]
    public async Task SettingsData_ViaVariables_DotNotation()
    {
        // Store via DataSource
        await _app.SettingsStore.Set("settings", "TestKey", new global::app.data.@this("TestKey", "TestValue"));

        // Resolve via User Variables dot notation (simulates %Settings.TestKey% in PLang code)
        var result = _app.User.Context.Variables.Get("Settings.TestKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("TestValue");
    }

    [Test]
    public async Task Settings_BarePath_ReturnsNotFound()
    {
        // %Settings% alone is meaningless — there's no Data object for "all settings".
        // The navigable resolver returns NotFound when path remainder is empty.
        var bare = _app.User.Context.Variables.Get("Settings");
        await Assert.That(bare).IsNotNull();
        await Assert.That(bare!.IsInitialized).IsFalse();
    }

    [Test]
    public async Task SettingsData_SetThenGetChild_ReflectsLatestValue()
    {
        await _app.SettingsStore.Set("settings", "ApiKey", new global::app.data.@this("ApiKey", "old-value"));
        var first = _app.User.Context.Variables.Get("Settings.ApiKey");
        await Assert.That(first!.Value?.ToString()).IsEqualTo("old-value");

        await _app.SettingsStore.Set("settings", "ApiKey", new global::app.data.@this("ApiKey", "new-value"));
        var second = _app.User.Context.Variables.Get("Settings.ApiKey");
        await Assert.That(second!.Value?.ToString()).IsEqualTo("new-value");
    }

    [Test]
    public async Task SettingsHandler_Set_ThenGetViaSettingsData()
    {
        // Use the settings.set action handler
        var context = _app.System.Context;
        var handler = new global::app.modules.settings.Set
        {
            Context = context,
            Key = "HandlerKey",
            Value = new global::app.data.@this("", "HandlerValue")        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        // Verify via User Variables (what PLang code uses)
        var setting = _app.User.Context.Variables.Get("Settings.HandlerKey");
        await Assert.That(setting).IsNotNull();
        await Assert.That(setting!.Value?.ToString()).IsEqualTo("HandlerValue");
    }

    [Test]
    public async Task SettingsHandler_Get_ExistingKey_ReturnsValue()
    {
        await _app.SettingsStore.Set("settings", "TestKey", new global::app.data.@this("TestKey", "TestValue"));

        var handler = new global::app.modules.settings.Get
        {
            Context = _app.System.Context,
            Key = "TestKey"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("TestValue");
    }

    [Test]
    public async Task SettingsHandler_Get_MissingKey_ReturnsAskError()
    {
        var handler = new global::app.modules.settings.Get
        {
            Context = _app.System.Context,
            Key = "MissingKey"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error is AskError).IsTrue();
    }

    [Test]
    public async Task SettingsHandler_Remove_DeletesKey()
    {
        await _app.SettingsStore.Set("settings", "ToRemove", new global::app.data.@this("ToRemove", "value"));

        var handler = new global::app.modules.settings.Remove
        {
            Context = _app.System.Context,
            Key = "ToRemove"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        // Verify removed
        var getResult = await _app.SettingsStore.Get("settings", "ToRemove");
        await Assert.That(getResult.Value).IsNull();
    }

    [Test]
    public async Task ActorDataSource_IsCreatedLazily()
    {
        // Accessing DataSource should create the .db directory
        var ds = _app.SettingsStore;
        await Assert.That(ds).IsNotNull();

        var dbDir = _app.FileSystem.Path.Combine(_tempDir, ".db");
        await Assert.That(_app.FileSystem.Directory.Exists(dbDir)).IsTrue();
    }

    // --- Nested settings path test ---

    [Test]
    public async Task SettingsData_NestedPath_NavigatesJsonObject()
    {
        // Store a JSON object as a setting
        var config = new Dictionary<string, object> { ["SubKey"] = "nested-value", ["Other"] = 42 };
        await _app.SettingsStore.Set("settings", "Config", new global::app.data.@this("Config", config));

        // Resolve %Settings.Config.SubKey% via User Variables
        var result = _app.User.Context.Variables.Get("Settings.Config.SubKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("nested-value");
    }

    // --- Variables.Clone preserves the Settings navigable mount ---

    [Test]
    public async Task Variables_Clone_PreservesSettingsData()
    {
        // Store a setting
        await _app.SettingsStore.Set("settings", "CloneKey", new global::app.data.@this("CloneKey", "clone-value"));

        // Clone the User Variables
        var cloned = _app.User.Context.Variables.Clone();

        // Settings should still work in the cloned stack
        var result = cloned.Get("Settings.CloneKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("clone-value");
    }

    [Test]
    public async Task Variables_Clone_SettingsData_MissingKey_ReturnsAskError()
    {
        // Clone the User Variables
        var cloned = _app.User.Context.Variables.Clone();

        // Missing key in cloned stack should still return AskError
        var result = cloned.Get("Settings.MissingInClone");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsFalse();
        await Assert.That(result.Error is AskError).IsTrue();
    }

    // --- Error propagation integration test ---

    [Test]
    public async Task ErrorPropagation_VariablesGet_SettingsMissing_ReturnsAskError()
    {
        // This simulates what the source generator's __Resolve<T> does:
        // 1. Gets a parameter with value "%Settings.MissingKey%"
        // 2. Regex matches the full variable: Settings.MissingKey
        // 3. Calls __variables.Get("Settings.MissingKey")
        // 4. Checks if result is non-null and !Success → sets __resolutionError
        var variables = _app.User.Context.Variables;

        // This is the exact call the generated code makes
        var resolved = variables.Get("Settings.MissingKey");

        // The generated code checks: if (__resolved != null && !__resolved.Success)
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsFalse();

        // Verify the error is AskError (so runtime can prompt user)
        await Assert.That(resolved.Error).IsNotNull();
        await Assert.That(resolved.Error is AskError).IsTrue();
        var askError = (AskError)resolved.Error!;
        await Assert.That(askError.DataKey).IsEqualTo("MissingKey");
    }

    [Test]
    public async Task ErrorPropagation_VariablesGet_SettingsExists_ReturnsSuccess()
    {
        await _app.SettingsStore.Set("settings", "ApiKey", new global::app.data.@this("ApiKey", "sk-real-key"));
        var variables = _app.User.Context.Variables;

        // Same call path as generated code
        var resolved = variables.Get("Settings.ApiKey");

        // Generated code checks: if (__resolved != null && !__resolved.Success) — should NOT trigger
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsTrue();
        await Assert.That(resolved.Value?.ToString()).IsEqualTo("sk-real-key");
    }

    // --- Store-level error path ---

    [Test]
    public async Task Settings_CorruptDatabase_ReturnsSettingsError()
    {
        // Trigger DataSource creation so the DB file exists
        _ = _app.SettingsStore;

        // Corrupt the database file — overwrite with garbage
        var dbPath = System.IO.Path.Combine(_tempDir, ".db", "system.sqlite");
        System.IO.File.WriteAllText(dbPath, "NOT A VALID SQLITE DATABASE FILE");

        // Settings.Get should surface the SettingsError from the store,
        // not throw and not return AskError.
        var resolved = _app.User.Context.Variables.Get("Settings.AnyKey");
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsFalse();
        await Assert.That(resolved.Error is SettingsError).IsTrue();
    }

    // --- Shared Settings instance across actors ---

    [Test]
    public async Task Settings_SharedInstanceAcrossAllActors()
    {
        // app.Settings is a singleton; the navigable resolver each actor's
        // Variables registers closes over it. Identity is the test.
        await Assert.That(ReferenceEquals(_app.Settings, _app.Settings)).IsTrue();
        // Both actors' Variables resolve %Settings.X% through the same app.Settings.
        await _app.SettingsStore.Set("settings", "SharedKey", new global::app.data.@this("SharedKey", "shared-value"));
        var fromUser = _app.User.Context.Variables.Get("Settings.SharedKey");
        var fromSystem = _app.System.Context.Variables.Get("Settings.SharedKey");
        await Assert.That(fromUser?.Value?.ToString()).IsEqualTo("shared-value");
        await Assert.That(fromSystem?.Value?.ToString()).IsEqualTo("shared-value");
    }

    [Test]
    public async Task SettingsData_SetViaSystem_ReadableFromUserContext()
    {
        // Store via System DataSource (the backing store)
        await _app.SettingsStore.Set("settings", "SharedKey", new global::app.data.@this("SharedKey", "shared-value"));

        // Read from User context (what PLang code actually uses)
        var result = _app.User.Context.Variables.Get("Settings.SharedKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("shared-value");

        // Read from Service context (should also work)
        var serviceResult = _app.System.Context.Variables.Get("Settings.SharedKey");
        await Assert.That(serviceResult).IsNotNull();
        await Assert.That(serviceResult!.Success).IsTrue();
        await Assert.That(serviceResult.Value?.ToString()).IsEqualTo("shared-value");
    }
}
