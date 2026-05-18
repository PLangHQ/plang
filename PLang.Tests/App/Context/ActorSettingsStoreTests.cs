using global::app.actor.context;
using global::app.variables;

namespace PLang.Tests.App.Context;

/// <summary>
/// Coverage for the app-level SettingsStore. Per-actor allocation was dead drift
/// — only System's store ever had real consumers. After stage 13's settings
/// rework, there's a single shared <c>app.SettingsStore</c> backed by
/// <c>.db/system.sqlite</c> (or in-memory under Testing).
/// </summary>
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
    public async Task SettingsStore_DuringBuilding_PersistsAcrossEngineInstances()
    {
        // Build mode → on-disk system.sqlite — survives App lifetime so
        // LLM cache and other persistent system data live across builds.
        await using (var engine = new global::app.@this(_testDir))
        {
            engine.Builder.IsEnabled = true;
            await engine.SettingsStore.Set("LlmCache", "testkey", Data.Ok("cached_response"));
        }

        await using (var engine2 = new global::app.@this(_testDir))
        {
            engine2.Builder.IsEnabled = true;
            var result = await engine2.SettingsStore.Get("LlmCache", "testkey");
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.ToString()).IsEqualTo("cached_response");
        }
    }

    [Test]
    public async Task SettingsStore_DuringTesting_IsolatedPerEngineInstance()
    {
        // During testing, the store is in-memory scoped by App.Id so per-test
        // Apps never share state. SQLite's shared-cache merges in-memory dbs
        // with identical DataSource names, so the App.Id scoping is load-bearing.
        await using (var engine = new global::app.@this(_testDir))
        {
            engine.Tester.IsEnabled = true;
            await engine.SettingsStore.Set("LlmCache", "testkey", Data.Ok("cached_response"));
        }

        await using (var engine2 = new global::app.@this(_testDir))
        {
            engine2.Tester.IsEnabled = true;
            var result = await engine2.SettingsStore.Get("LlmCache", "testkey");
            await Assert.That(result.Value).IsNull();
        }
    }
}
