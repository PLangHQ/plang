using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using Verb = global::app.type.permission.Verb;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// <summary>
/// <c>.Absolute</c> discipline.
/// </summary>
public class AbsoluteDisciplineTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(action.Context.Ok(_answer));
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
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "db.sqlite");
        var p = new FilePath(outOfRoot, app.User.Context);
        var auth = await p.Authorize(global::app.type.permission.Verb.Write);
        await auth.IsFailure();
    }

    [Test] public async Task TakeOverApi_AuthorizeFirst_InRootGrant_AllowsAbsoluteUse()
    {
        var app = NewApp(out var root);
        var p = new FilePath(System.IO.Path.Combine(root, "db.sqlite"), app.User.Context);
        var auth = await p.Authorize(global::app.type.permission.Verb.Write);
        await auth.IsSuccess();
        // .Absolute is now safe to read.
        await Assert.That(p.Absolute).IsNotNull();
    }

    [Test] public async Task MutationGuard_RemovingAuthorizeBeforeAbsolute_BreaksThisTest()
    {
        // Mutation-test placeholder: if the Sqlite ctor's Authorize call were
        // removed, an out-of-root db path would open without permission. With
        // Authorize in place + a denied actor, it must throw.
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "db.sqlite");
        var dbPath = new FilePath(outOfRoot, app.User.Context);
        bool threw = false;
        try { using var _ = await global::app.module.settings.Sqlite.CreateAsync(dbPath, app.User.Context); }
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
        await r.IsSuccess();
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
