using app.actor.context;
using app.variable;

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
        await using (var engine = global::PLang.Tests.TestApp.Plain(_testDir))
        {
            engine.Build = new global::app.module.action.build.@this(engine.System.Context);
            await (await engine.SettingsStore).Set("LlmCache", "testkey", engine.User.Context.Ok("cached_response"));
        }

        await using (var engine2 = global::PLang.Tests.TestApp.Plain(_testDir))
        {
            engine2.Build = new global::app.module.action.build.@this(engine2.System.Context);
            var result = await (await engine2.SettingsStore).Get<global::app.type.item.@this>("LlmCache", "testkey");
            await Assert.That((await result.Value())).IsNotNull();
            await Assert.That((await result.Value())!.ToString()).IsEqualTo("cached_response");
        }
    }

    [Test]
    public async Task SettingsStore_DuringTesting_IsolatedPerEngineInstance()
    {
        // During testing, the store is in-memory scoped by App.Id so per-test
        // Apps never share state. SQLite's shared-cache merges in-memory dbs
        // with identical DataSource names, so the App.Id scoping is load-bearing.
        await using (var engine = global::PLang.Tests.TestApp.Plain(_testDir))
        {
            engine.Test = new global::app.test.list.@this(engine.System.Context);
            await (await engine.SettingsStore).Set("LlmCache", "testkey", engine.User.Context.Ok("cached_response"));
        }

        await using (var engine2 = global::PLang.Tests.TestApp.Plain(_testDir))
        {
            engine2.Test = new global::app.test.list.@this(engine2.System.Context);
            var result = await (await engine2.SettingsStore).Get<global::app.type.item.@this>("LlmCache", "testkey");
            // A missing key yields an empty value (the plang null/absent citizen),
            // never C# null — assert emptiness the plang way, not TUnit IsNull.
            await Assert.That(await (await result.Value())!.IsEmpty()).IsTrue();
        }
    }

    [Test]
    public async Task SettingsStore_Identity_RoundTripsViaGetOfIdentity()
    {
        // The seam every module author uses: store an Identity, read it back typed.
        // The store persists the Store view (incl. [Sensitive] PrivateKey) and hands
        // back a Data<Identity> face; the typed lift (.Value()) reconstructs the item.
        await using var engine = global::PLang.Tests.TestApp.Plain(_testDir);
        engine.Test = new global::app.test.list.@this(engine.System.Context);

        var original = new global::app.module.action.identity.Identity("work")
            { PublicKey = "pub-abc", PrivateKey = "priv-xyz", IsDefault = true };
        await (await engine.SettingsStore).Set("identity", "work",
            new Data("work", original));

        var data   = await (await engine.SettingsStore).Get<global::app.module.action.identity.Identity>("identity", "work");
        var loaded = await data.Value();

        await Assert.That((object?)loaded).IsNotNull();
        await Assert.That(loaded!.Name).IsEqualTo("work");
        await Assert.That(loaded.PublicKey).IsEqualTo("pub-abc");
        await Assert.That(loaded.PrivateKey).IsEqualTo("priv-xyz");
        await Assert.That(loaded.IsDefault).IsTrue();
    }
}
