using global::App.Actor.Context;
using global::App.Settings;
using global::App.Errors;
using global::App.Variables;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.settings;

public class SettingsDataTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_settings_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    [Test]
    public async Task SettingsData_GetChild_ReturnsStoredValue()
    {
        // Store a setting via the System actor's DataSource
        await _engine.System.SettingsStore.Set("settings", "ApiKey", new SettingsVariable("ApiKey", "sk-test-123"));

        // SettingsVariable is registered on User actor Variables (same as PLang code uses)
        var settingsData = _engine.Context.Variables.Get("Settings");
        await Assert.That(settingsData).IsNotNull();

        var child = settingsData!.GetChild("ApiKey");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Success).IsTrue();
        await Assert.That(child.Value?.ToString()).IsEqualTo("sk-test-123");
    }

    [Test]
    public async Task SettingsData_GetChild_MissingKey_ReturnsAskError()
    {
        var settingsData = _engine.Context.Variables.Get("Settings");
        await Assert.That(settingsData).IsNotNull();

        var child = settingsData!.GetChild("NonExistentKey");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Success).IsFalse();
        await Assert.That(child.Error is AskError).IsTrue();

        var askError = (AskError)child.Error!;
        await Assert.That(askError.Table).IsEqualTo("settings");
        await Assert.That(askError.DataKey).IsEqualTo("NonExistentKey");
    }

    [Test]
    public async Task SettingsData_ViaVariables_DotNotation()
    {
        // Store via DataSource
        await _engine.System.SettingsStore.Set("settings", "TestKey", new SettingsVariable("TestKey", "TestValue"));

        // Resolve via User Variables dot notation (simulates %Settings.TestKey% in PLang code)
        var result = _engine.Context.Variables.Get("Settings.TestKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("TestValue");
    }

    [Test]
    public async Task SettingsData_EmptyPath_ReturnsSelf()
    {
        var settingsData = _engine.Context.Variables.Get("Settings");
        await Assert.That(settingsData).IsNotNull();

        var child = settingsData!.GetChild("");
        await Assert.That(child).IsNotNull();
        // Should return itself
        await Assert.That(child).IsEqualTo(settingsData);
    }

    [Test]
    public async Task SettingsData_SetThenGetChild_ReflectsLatestValue()
    {
        await _engine.System.SettingsStore.Set("settings", "ApiKey", new SettingsVariable("ApiKey", "old-value"));
        var first = _engine.Context.Variables.Get("Settings.ApiKey");
        await Assert.That(first!.Value?.ToString()).IsEqualTo("old-value");

        await _engine.System.SettingsStore.Set("settings", "ApiKey", new SettingsVariable("ApiKey", "new-value"));
        var second = _engine.Context.Variables.Get("Settings.ApiKey");
        await Assert.That(second!.Value?.ToString()).IsEqualTo("new-value");
    }

    [Test]
    public async Task SettingsHandler_Set_ThenGetViaSettingsData()
    {
        // Use the settings.set action handler
        var context = _engine.System.Context;
        var handler = new global::App.modules.settings.Set
        {
            Context = context,
            Key = "HandlerKey",
            Value = "HandlerValue"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        // Verify via User Variables (what PLang code uses)
        var setting = _engine.Context.Variables.Get("Settings.HandlerKey");
        await Assert.That(setting).IsNotNull();
        await Assert.That(setting!.Value?.ToString()).IsEqualTo("HandlerValue");
    }

    [Test]
    public async Task SettingsHandler_Get_ExistingKey_ReturnsValue()
    {
        await _engine.System.SettingsStore.Set("settings", "TestKey", new SettingsVariable("TestKey", "TestValue"));

        var handler = new global::App.modules.settings.Get
        {
            Context = _engine.System.Context,
            Key = "TestKey"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("TestValue");
    }

    [Test]
    public async Task SettingsHandler_Get_MissingKey_ReturnsAskError()
    {
        var handler = new global::App.modules.settings.Get
        {
            Context = _engine.System.Context,
            Key = "MissingKey"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error is AskError).IsTrue();
    }

    [Test]
    public async Task SettingsHandler_Remove_DeletesKey()
    {
        await _engine.System.SettingsStore.Set("settings", "ToRemove", new SettingsVariable("ToRemove", "value"));

        var handler = new global::App.modules.settings.Remove
        {
            Context = _engine.System.Context,
            Key = "ToRemove"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        // Verify removed
        var getResult = await _engine.System.SettingsStore.Get("settings", "ToRemove");
        await Assert.That(getResult.Value).IsNull();
    }

    [Test]
    public async Task ActorDataSource_IsCreatedLazily()
    {
        // Accessing DataSource should create the .db directory
        var ds = _engine.System.SettingsStore;
        await Assert.That(ds).IsNotNull();

        var dbDir = _engine.FileSystem.Path.Combine(_tempDir, ".db");
        await Assert.That(_engine.FileSystem.Directory.Exists(dbDir)).IsTrue();
    }

    // --- Nested settings path test ---

    [Test]
    public async Task SettingsData_NestedPath_NavigatesJsonObject()
    {
        // Store a JSON object as a setting
        var config = new Dictionary<string, object> { ["SubKey"] = "nested-value", ["Other"] = 42 };
        await _engine.System.SettingsStore.Set("settings", "Config", new SettingsVariable("Config", config));

        // Resolve %Settings.Config.SubKey% via User Variables
        var result = _engine.Context.Variables.Get("Settings.Config.SubKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("nested-value");
    }

    // --- Variables.Clone preserves SettingsVariable ---

    [Test]
    public async Task Variables_Clone_PreservesSettingsData()
    {
        // Store a setting
        await _engine.System.SettingsStore.Set("settings", "CloneKey", new SettingsVariable("CloneKey", "clone-value"));

        // Clone the User Variables
        var cloned = _engine.Context.Variables.Clone();

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
        var cloned = _engine.Context.Variables.Clone();

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
        // This simulates what LazyParamsGenerator's __Resolve<T> does:
        // 1. Gets a parameter with value "%Settings.MissingKey%"
        // 2. Regex matches the full variable: Settings.MissingKey
        // 3. Calls __variables.Get("Settings.MissingKey")
        // 4. Checks if result is non-null and !Success → sets __resolutionError
        var variables = _engine.Context.Variables;

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
        await _engine.System.SettingsStore.Set("settings", "ApiKey", new SettingsVariable("ApiKey", "sk-real-key"));
        var variables = _engine.Context.Variables;

        // Same call path as generated code
        var resolved = variables.Get("Settings.ApiKey");

        // Generated code checks: if (__resolved != null && !__resolved.Success) — should NOT trigger
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsTrue();
        await Assert.That(resolved.Value?.ToString()).IsEqualTo("sk-real-key");
    }

    // --- SettingsVariable DataSource error path (F4) ---

    [Test]
    public async Task SettingsData_GetChild_CorruptDatabase_ReturnsSettingsError()
    {
        // Trigger DataSource creation so the DB file exists
        _ = _engine.System.SettingsStore;

        // Corrupt the database file — overwrite with garbage
        var dbPath = System.IO.Path.Combine(_tempDir, ".db", "system.sqlite");
        System.IO.File.WriteAllText(dbPath, "NOT A VALID SQLITE DATABASE FILE");

        // SettingsVariable.GetChild should return the DataSource error (line 54-55),
        // not throw and not return AskError
        var settingsData = _engine.Context.Variables.Get("Settings");
        var child = settingsData!.GetChild("AnyKey");

        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Success).IsFalse();
        await Assert.That(child.Error is SettingsError).IsTrue();
    }

    // --- Shared SettingsVariable across actors ---

    [Test]
    public async Task SettingsData_SameObjectAcrossAllActors()
    {
        // All actors should share the exact same SettingsVariable instance
        var userSettings = _engine.User.Context.Variables.Get("Settings");
        var systemSettings = _engine.System.Context.Variables.Get("Settings");
        var serviceSettings = _engine.Service.Context.Variables.Get("Settings");

        await Assert.That(userSettings).IsNotNull();
        await Assert.That(systemSettings).IsNotNull();
        await Assert.That(serviceSettings).IsNotNull();

        // Reference equality — same object
        await Assert.That(ReferenceEquals(userSettings, systemSettings)).IsTrue();
        await Assert.That(ReferenceEquals(userSettings, serviceSettings)).IsTrue();
    }

    [Test]
    public async Task SettingsData_SetViaSystem_ReadableFromUserContext()
    {
        // Store via System DataSource (the backing store)
        await _engine.System.SettingsStore.Set("settings", "SharedKey", new SettingsVariable("SharedKey", "shared-value"));

        // Read from User context (what PLang code actually uses)
        var result = _engine.Context.Variables.Get("Settings.SharedKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("shared-value");

        // Read from Service context (should also work)
        var serviceResult = _engine.Service.Context.Variables.Get("Settings.SharedKey");
        await Assert.That(serviceResult).IsNotNull();
        await Assert.That(serviceResult!.Success).IsTrue();
        await Assert.That(serviceResult.Value?.ToString()).IsEqualTo("shared-value");
    }
}
