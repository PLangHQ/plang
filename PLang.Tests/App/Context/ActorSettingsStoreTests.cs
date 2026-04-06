using App.Context;
using App.Variables;
using App.Settings;

namespace PLang.Tests.App.Context;

public class ActorSettingsStoreTests
{
    private string _testDir = null!;

    [Before(Test)]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Test]
    public async Task SystemActor_DuringBuilding_PersistsCacheAcrossEngineInstances()
    {
        // First engine — write to System settings store
        await using (var engine = new App.@this(_testDir))
        {
            engine.Building.IsEnabled = true;
            await engine.System.SettingsStore.Set("LlmCache", "testkey", Data.Ok("cached_response"));
        }

        // Second engine — System should still have the data (on-disk)
        await using (var engine2 = new App.@this(_testDir))
        {
            engine2.Building.IsEnabled = true;
            var result = await engine2.System.SettingsStore.Get("LlmCache", "testkey");
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.ToString()).IsEqualTo("cached_response");
        }
    }

    [Test]
    public async Task UserActor_DuringBuilding_DoesNotPersistAcrossEngineInstances()
    {
        // First engine — write to User settings store
        await using (var engine = new App.@this(_testDir))
        {
            engine.Building.IsEnabled = true;
            await engine.User.SettingsStore.Set("TestTable", "key1", Data.Ok("temporary"));
        }

        // Second engine — User data should be gone (in-memory)
        await using (var engine2 = new App.@this(_testDir))
        {
            engine2.Building.IsEnabled = true;
            var result = await engine2.User.SettingsStore.Get("TestTable", "key1");
            await Assert.That(result.Value).IsNull();
        }
    }

    [Test]
    public async Task SystemActor_DuringTesting_PersistsCacheAcrossEngineInstances()
    {
        await using (var engine = new App.@this(_testDir))
        {
            engine.Testing.IsEnabled = true;
            await engine.System.SettingsStore.Set("LlmCache", "testkey", Data.Ok("cached_response"));
        }

        await using (var engine2 = new App.@this(_testDir))
        {
            engine2.Testing.IsEnabled = true;
            var result = await engine2.System.SettingsStore.Get("LlmCache", "testkey");
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.ToString()).IsEqualTo("cached_response");
        }
    }

    [Test]
    public async Task UserActor_DuringTesting_DoesNotPersistAcrossEngineInstances()
    {
        await using (var engine = new App.@this(_testDir))
        {
            engine.Testing.IsEnabled = true;
            await engine.User.SettingsStore.Set("TestTable", "key1", Data.Ok("temporary"));
        }

        await using (var engine2 = new App.@this(_testDir))
        {
            engine2.Testing.IsEnabled = true;
            var result = await engine2.User.SettingsStore.Get("TestTable", "key1");
            await Assert.That(result.Value).IsNull();
        }
    }
}
