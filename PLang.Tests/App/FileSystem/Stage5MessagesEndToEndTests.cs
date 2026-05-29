using Path = global::app.type.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.type.path.permission.@this;
using Verb = global::app.type.path.permission.verb.@this;
using Read = global::app.type.path.permission.verb.Read;
using MatchMode = global::app.type.path.permission.Match;

namespace PLang.Tests.App.FileSystem;

/// End-to-end Messages flow. Six scenarios from the architect's
/// stage-5 doc, exercised via the Stage 4 v2 surface (Path.Operations.cs).
/// PLang `.test.goal` versions under Tests/Permission/ are intent-only until
/// the file action handlers (modules/file/read.cs etc.) migrate to call
/// Path.Authorize as their first step (follow-up work — those handlers are
/// sync today and the migration needs an async refactor).
public class Stage5MessagesEndToEndTests
{
    private static (global::app.@this app, string foreignPath) Setup(string answer)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-s5-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        app.User.Channels.Register(new CannedChannel(answer));

        // Simulate /apps/Email/system.sqlite living outside the app root.
        var foreignDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-email-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(foreignDir);
        var foreignFile = System.IO.Path.Combine(foreignDir, "system.sqlite");
        System.IO.File.WriteAllText(foreignFile, "fake-sqlite-bytes");
        return (app, foreignFile);
    }

    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public int AskCount { get; private set; }
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            AskCount++;
            return Task.FromResult(global::app.data.@this.Ok(_answer));
        }
    }

    private sealed class StatelessChannel : global::app.channel.message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    [Test] public async Task Scenario1_NoGrantSuspends_StatelessChannelReturnsDataAsk()
    {
        var (app, _) = Setup("UNUSED");
        // Override channel: stateless.
        app.User.Channels.Register(new StatelessChannel());
        var foreignFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-email-x", "system.sqlite");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(foreignFile)!);
        System.IO.File.WriteAllText(foreignFile, "data");

        var path = new Path(foreignFile, app.User.Context);
        var result = await path.ReadText();
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test] public async Task Scenario2_GrantAStoresPersisted_AnswerPathSignsAndAdds()
    {
        var (app, foreignFile) = Setup("a");
        var path = new Path(foreignFile, app.User.Context);
        var result = await path.ReadText();
        await Assert.That(result.Success).IsTrue();
        // Grant landed and is signed (persisted).
        var found = await app.User.Permission.Find(path, new Verb { Read = new Read() });
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Signature).IsNotNull();
    }

    [Test] public async Task Scenario3_ImmediateRereadSkipsPrompt_FindCoversNoAsk()
    {
        var (app, foreignFile) = Setup("a");
        var path = new Path(foreignFile, app.User.Context);
        var ch = (CannedChannel)app.User.Channels.Resolve("input")!;

        await path.ReadText(); // grants via prompt
        var asksAfterFirst = ch.AskCount;
        var result = await path.ReadText(); // no prompt — grant covers
        await Assert.That(ch.AskCount).IsEqualTo(asksAfterFirst);
        await Assert.That(result.Success).IsTrue();
    }

    [Test] public async Task Scenario4_RestartStillNoPrompt_PersistedGrantSurvivesNewApp()
    {
        // Set up app1 on a root, grant via "a" so the row is signed and lands
        // in sqlite, then construct app2 on the same root. The persisted grant
        // is identified by (Actor + Path + Verb) — App.Id no longer scopes
        // grants — so app2's Find must hit without a fresh prompt.
        var (app1, foreignFile) = Setup("a");
        var root = app1.AbsolutePath;
        var path1 = new Path(foreignFile, app1.User.Context);
        var firstRead = await path1.ReadText();
        await Assert.That(firstRead.Success).IsTrue();

        // App #2 on the same root. Channel here has zero "a" answers — any
        // prompt that fires means the persisted grant was missed.
        var app2 = new global::app.@this(root);
        var statelessProbe = new StatelessChannel();
        app2.User.Channels.Register(statelessProbe);
        var path2 = new Path(foreignFile, app2.User.Context);
        var secondRead = await path2.ReadText();
        await Assert.That(secondRead.Success).IsTrue();
        // No Exit-typed bubble (no prompt) — the grant covered the request.
        await Assert.That(secondRead.Type?.Value).IsNotEqualTo("ask");
    }

    /// Persisted "always allow" grants must outlive the wire-freshness
    /// window (Config.TimeoutMs, default 5 min). Without SkipFreshnessCheck
    /// on grant verification, this re-read after advancing NowUtc by 10
    /// minutes would re-prompt — the user would see "always allow" expire
    /// after 5 minutes despite the docs claiming permanence.
    [Test] public async Task Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow()
    {
        var (app1, foreignFile) = Setup("a");
        var root = app1.AbsolutePath;
        var path1 = new Path(foreignFile, app1.User.Context);
        var firstRead = await path1.ReadText();
        await Assert.That(firstRead.Success).IsTrue();

        // Advance clock by 10 minutes — past the default 5-minute
        // Config.TimeoutMs window that would otherwise expire the signature.
        var app2 = new global::app.@this(root);
        var statelessProbe = new StatelessChannel();
        app2.User.Channels.Register(statelessProbe);
        // Replace the DynamicData NowUtc with a static Data (the DynamicData's
        // override Value getter ignores `_value`, so Set("NowUtc", offset)
        // would be a no-op — we must replace with a fresh Data instance).
        app2.User.Context.Variables.Set(
            new global::app.data.@this("NowUtc", DateTimeOffset.UtcNow.AddMinutes(10),
                global::app.type.@this.DateTime));

        var path2 = new Path(foreignFile, app2.User.Context);
        var secondRead = await path2.ReadText();
        await Assert.That(secondRead.Success).IsTrue();
        await Assert.That(secondRead.Type?.Value).IsNotEqualTo("ask");
    }

    /// Nonce-replay half of the persisted-grant contract: a persisted grant
    /// is re-verified on every Find (SettingsStore.GetAll yields a fresh
    /// Data each call, so the per-instance VerifiedFlag cache does not
    /// carry across reads). Without SkipFreshnessCheck neutralising the
    /// nonce-replay step, the second verification inside one app would hit
    /// NonceReplay and re-prompt. Pairs with the WireFreshnessWindow test,
    /// which gates only the wire-freshness step.
    [Test] public async Task Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt()
    {
        var (app1, foreignFile) = Setup("a");
        var root = app1.AbsolutePath;
        var path1 = new Path(foreignFile, app1.User.Context);
        await Assert.That((await path1.ReadText()).Success).IsTrue();   // create persisted grant

        // app2: two reads. Each Find re-deserializes the grant → two real
        // VerifySignature passes → step 4 would NonceReplay the second.
        var app2 = new global::app.@this(root);
        app2.User.Channels.Register(new StatelessChannel());
        var path2 = new Path(foreignFile, app2.User.Context);
        var read1 = await path2.ReadText();   // verify #1 — nonce cached
        var read2 = await path2.ReadText();   // verify #2 — nonce replay if step 4 active
        await Assert.That(read1.Success).IsTrue();
        await Assert.That(read1.Type?.Value).IsNotEqualTo("ask");
        await Assert.That(read2.Success).IsTrue();
        await Assert.That(read2.Type?.Value).IsNotEqualTo("ask");
    }

    [Test] public async Task Scenario5_RevokeReprompts_AfterRevokeFreshPromptFires()
    {
        var (app, foreignFile) = Setup("a");
        var path = new Path(foreignFile, app.User.Context);
        var ch = (CannedChannel)app.User.Channels.Resolve("input")!;

        await path.ReadText();              // initial grant
        var asksBeforeRevoke = ch.AskCount;

        // Revoke the persisted grant.
        var permission = new PermissionRecord(app.User.Name, path.Absolute, Verb.AllowAll(), MatchMode.Exact);
        await app.User.Permission.Revoke(permission);

        await path.ReadText();              // fresh prompt fires
        await Assert.That(ch.AskCount).IsGreaterThan(asksBeforeRevoke);
    }

    [Test] public async Task Scenario6_NarrowedGrantRejectsWiderRequest()
    {
        var (app, foreignFile) = Setup("a");
        // Pre-seed a narrowed grant (Read with Metadata=false) — does NOT cover
        // a request that needs Metadata.
        var narrowedVerb = new Verb { Read = new Read(Recursive: true, Metadata: false), Write = null, Delete = null };
        var narrowGrant = new global::app.data.@this<PermissionRecord>("",
            new PermissionRecord(app.User.Name, foreignFile, narrowedVerb, MatchMode.Exact))
        { Context = app.User.Context };
        narrowGrant.EnsureSigned();
        await app.User.Permission.Add(narrowGrant);

        // Stat needs Metadata=true; the narrowed Read grant doesn't cover it.
        // Authorize asks; the CannedChannel answers "a" and a wider grant lands.
        var path = new Path(foreignFile, app.User.Context);
        var ch = (CannedChannel)app.User.Channels.Resolve("input")!;
        var statResult = await path.Stat();
        await Assert.That(statResult.Success).IsTrue();
        await Assert.That(ch.AskCount).IsGreaterThan(0); // prompt fired despite the narrow grant
    }
}
