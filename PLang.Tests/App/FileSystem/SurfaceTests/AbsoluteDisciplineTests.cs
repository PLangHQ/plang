using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using Verb = global::app.types.path.permission.verb.@this;
using Write = global::app.types.path.permission.verb.Write;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// <summary>
/// Stage 5 — Batch 8. <c>.Absolute</c> discipline (D13).
/// </summary>
public class AbsoluteDisciplineTests
{
    private sealed class CannedChannel : global::app.channels.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channels.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> AskCore(global::app.modules.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-abs-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task TakeOverApi_AuthorizeFirst_OutOfRootDenial_PreventsAbsoluteUse()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "db.sqlite");
        var p = new FilePath(outOfRoot, app.User.Context);
        var auth = await p.Authorize(new Verb { Write = new Write() });
        await Assert.That(auth.Success).IsFalse();
    }

    [Test] public async Task TakeOverApi_AuthorizeFirst_InRootGrant_AllowsAbsoluteUse()
    {
        var app = NewApp(out var root);
        var p = new FilePath(System.IO.Path.Combine(root, "db.sqlite"), app.User.Context);
        var auth = await p.Authorize(new Verb { Write = new Write() });
        await Assert.That(auth.Success).IsTrue();
        // .Absolute is now safe to read.
        await Assert.That(p.Absolute).IsNotNull();
    }

    [Test] public async Task MutationGuard_RemovingAuthorizeBeforeAbsolute_BreaksThisTest()
    {
        // Mutation-test placeholder: if the Sqlite ctor's Authorize call were
        // removed, an out-of-root db path would open without permission. With
        // Authorize in place + a denied actor, it must throw.
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "db.sqlite");
        var dbPath = new FilePath(outOfRoot, app.User.Context);
        bool threw = false;
        try { using var _ = new global::app.modules.settings.Sqlite(dbPath); }
        catch (System.InvalidOperationException) { threw = true; }
        await Assert.That(threw).IsTrue();
    }

    [Test] public async Task PathInternals_ReachForAbsolute_IsAllowed_NoDiagnostic()
    {
        var app = NewApp(out var root);
        var p = new FilePath(System.IO.Path.Combine(root, "x.txt"), app.User.Context);
        // Path verbs internally use .Absolute — confirm a verb call succeeds
        // on an in-root Path (the .Absolute reach inside the verb is allowed).
        await p.WriteText("hi");
        var r = await p.ReadText();
        await Assert.That(r.Success).IsTrue();
    }

    [Test] public async Task DiagnosticString_UsesAbsolute_InErrorMessage_IsAllowed()
    {
        var app = NewApp(out var root);
        var p = new FilePath(System.IO.Path.Combine(root, "diag.txt"), app.User.Context);
        // ToString and string interpolation embed .Absolute — confirm no
        // gate fires (these are pure string ops, not IO).
        var msg = $"path: {p}";
        await Assert.That(msg).Contains("diag.txt");
    }
}
