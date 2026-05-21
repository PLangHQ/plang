using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Path = global::App.FileSystem.Path;
using PermissionRecord = global::App.FileSystem.Permission.@this;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using MatchMode = global::App.FileSystem.Permission.Match;

namespace PLang.Tests.App.FileSystem;

/// Stage 5 — End-to-end Messages flow. Six scenarios from the architect's
/// stage-5 doc, exercised via the Stage 4 v2 surface (Path.Operations.cs).
/// PLang `.test.goal` versions under Tests/Permission/ are intent-only until
/// the file action handlers (modules/file/read.cs etc.) migrate to call
/// Path.Authorize as their first step (follow-up work — those handlers are
/// sync today and the migration needs an async refactor).
public class Stage5MessagesEndToEndTests
{
    private static (global::App.@this app, string foreignPath) Setup(string answer)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-s5-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::App.@this(root);
        app.User.Channels.Register(new CannedChannel(answer));

        // Simulate /apps/Email/system.sqlite living outside the app root.
        var foreignDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-email-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(foreignDir);
        var foreignFile = System.IO.Path.Combine(foreignDir, "system.sqlite");
        System.IO.File.WriteAllText(foreignFile, "fake-sqlite-bytes");
        return (app, foreignFile);
    }

    private sealed class CannedChannel : global::App.Channels.Channel.@this
    {
        private readonly string _answer;
        public int AskCount { get; private set; }
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional; }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok((object?)null));
        public override Task<global::App.Data.@this> AskCore(global::App.modules.output.ask action, CancellationToken ct = default)
        {
            AskCount++;
            return Task.FromResult(global::App.Data.@this.Ok(_answer));
        }
    }

    private sealed class StatelessChannel : global::App.Channels.Channel.Message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional; }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::App.Data.@this.Ok((object?)null));
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
        await Assert.That(found!.RawSignature).IsNotNull();
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
        var app2 = new global::App.@this(root);
        var statelessProbe = new StatelessChannel();
        app2.User.Channels.Register(statelessProbe);
        var path2 = new Path(foreignFile, app2.User.Context);
        var secondRead = await path2.ReadText();
        await Assert.That(secondRead.Success).IsTrue();
        // No Exit-typed bubble (no prompt) — the grant covered the request.
        await Assert.That(secondRead.Type?.Value).IsNotEqualTo("ask");
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
        var narrowGrant = new global::App.Data.@this<PermissionRecord>("",
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
