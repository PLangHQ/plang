using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.DataSource;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.settings;

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
        await _engine.System.DataSource.Set("settings", "ApiKey", "sk-test-123");

        // SettingsData is auto-registered on System actor MemoryStack
        var settingsData = _engine.System.Context.MemoryStack.Get("Settings");
        await Assert.That(settingsData).IsNotNull();

        var child = settingsData!.GetChild("ApiKey");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Success).IsTrue();
        await Assert.That(child.Value?.ToString()).IsEqualTo("sk-test-123");
    }

    [Test]
    public async Task SettingsData_GetChild_MissingKey_ReturnsAskError()
    {
        var settingsData = _engine.System.Context.MemoryStack.Get("Settings");
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
    public async Task SettingsData_ViaMemoryStack_DotNotation()
    {
        // Store via DataSource
        await _engine.System.DataSource.Set("settings", "TestKey", "TestValue");

        // Resolve via MemoryStack dot notation (simulates %Settings.TestKey%)
        var result = _engine.System.Context.MemoryStack.Get("Settings.TestKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("TestValue");
    }

    [Test]
    public async Task SettingsData_EmptyPath_ReturnsSelf()
    {
        var settingsData = _engine.System.Context.MemoryStack.Get("Settings");
        await Assert.That(settingsData).IsNotNull();

        var child = settingsData!.GetChild("");
        await Assert.That(child).IsNotNull();
        // Should return itself
        await Assert.That(child).IsEqualTo(settingsData);
    }

    [Test]
    public async Task SettingsData_SetThenGetChild_ReflectsLatestValue()
    {
        await _engine.System.DataSource.Set("settings", "ApiKey", "old-value");
        var first = _engine.System.Context.MemoryStack.Get("Settings.ApiKey");
        await Assert.That(first!.Value?.ToString()).IsEqualTo("old-value");

        await _engine.System.DataSource.Set("settings", "ApiKey", "new-value");
        var second = _engine.System.Context.MemoryStack.Get("Settings.ApiKey");
        await Assert.That(second!.Value?.ToString()).IsEqualTo("new-value");
    }

    [Test]
    public async Task SettingsHandler_Set_ThenGetViaSettingsData()
    {
        // Use the settings.set action handler
        var context = _engine.System.Context;
        var handler = new PLang.Runtime2.actions.settings.Set
        {
            Context = context,
            Key = "HandlerKey",
            Value = "HandlerValue"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        // Verify via SettingsData
        var setting = _engine.System.Context.MemoryStack.Get("Settings.HandlerKey");
        await Assert.That(setting).IsNotNull();
        await Assert.That(setting!.Value?.ToString()).IsEqualTo("HandlerValue");
    }

    [Test]
    public async Task SettingsHandler_Get_ExistingKey_ReturnsValue()
    {
        await _engine.System.DataSource.Set("settings", "TestKey", "TestValue");

        var handler = new PLang.Runtime2.actions.settings.Get
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
        var handler = new PLang.Runtime2.actions.settings.Get
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
        await _engine.System.DataSource.Set("settings", "ToRemove", "value");

        var handler = new PLang.Runtime2.actions.settings.Remove
        {
            Context = _engine.System.Context,
            Key = "ToRemove"
        };

        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        // Verify removed
        var getResult = await _engine.System.DataSource.Get("settings", "ToRemove");
        await Assert.That(getResult.Value).IsNull();
    }

    [Test]
    public async Task ActorDataSource_IsCreatedLazily()
    {
        // Accessing DataSource should create the .db directory
        var ds = _engine.System.DataSource;
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
        await _engine.System.DataSource.Set("settings", "Config", config);

        // Resolve %Settings.Config.SubKey%
        var result = _engine.System.Context.MemoryStack.Get("Settings.Config.SubKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("nested-value");
    }

    // --- MemoryStack.Clone preserves SettingsData ---

    [Test]
    public async Task MemoryStack_Clone_PreservesSettingsData()
    {
        // Store a setting
        await _engine.System.DataSource.Set("settings", "CloneKey", "clone-value");

        // Clone the MemoryStack
        var cloned = _engine.System.Context.MemoryStack.Clone();

        // Settings should still work in the cloned stack
        var result = cloned.Get("Settings.CloneKey");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("clone-value");
    }

    [Test]
    public async Task MemoryStack_Clone_SettingsData_MissingKey_ReturnsAskError()
    {
        // Clone the MemoryStack
        var cloned = _engine.System.Context.MemoryStack.Clone();

        // Missing key in cloned stack should still return AskError
        var result = cloned.Get("Settings.MissingInClone");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Success).IsFalse();
        await Assert.That(result.Error is AskError).IsTrue();
    }

    // --- Error propagation integration test ---

    [Test]
    public async Task ErrorPropagation_MemoryStackGet_SettingsMissing_ReturnsAskError()
    {
        // This simulates what LazyParamsGenerator's __Resolve<T> does:
        // 1. Gets a parameter with value "%Settings.MissingKey%"
        // 2. Regex matches the full variable: Settings.MissingKey
        // 3. Calls __memoryStack.Get("Settings.MissingKey")
        // 4. Checks if result is non-null and !Success → sets __resolutionError
        var memoryStack = _engine.System.Context.MemoryStack;

        // This is the exact call the generated code makes
        var resolved = memoryStack.Get("Settings.MissingKey");

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
    public async Task ErrorPropagation_MemoryStackGet_SettingsExists_ReturnsSuccess()
    {
        await _engine.System.DataSource.Set("settings", "ApiKey", "sk-real-key");
        var memoryStack = _engine.System.Context.MemoryStack;

        // Same call path as generated code
        var resolved = memoryStack.Get("Settings.ApiKey");

        // Generated code checks: if (__resolved != null && !__resolved.Success) — should NOT trigger
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Success).IsTrue();
        await Assert.That(resolved.Value?.ToString()).IsEqualTo("sk-real-key");
    }
}
