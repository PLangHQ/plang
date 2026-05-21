using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Path = global::App.FileSystem.Path;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using Write = global::App.FileSystem.Permission.Verb.Write;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Stage 4 — Batch 9: Move/Copy ask each Path its respective verb. Both Ok
/// → operation proceeds. Either returns Data<Ask> → bundled `Ask` with one
/// question string covering both paths. On bundled "a", both grants land.
public class MoveCopyBundledConsentTests
{
    private static global::App.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-mc-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new global::App.@this(root);
    }

    private sealed class CapturingChannel : global::App.Channels.Channel.@this
    {
        public string LastQuestion { get; private set; } = "";
        public int AskCount { get; private set; }
        private readonly string _answer;
        public CapturingChannel(string answer)
        {
            _answer = answer; Name = "input";
            Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok((object?)null));
        public override Task<global::App.Data.@this> AskCore(global::App.modules.output.ask action, CancellationToken ct = default)
        {
            LastQuestion = action.Question?.Value ?? "";
            AskCount++;
            return Task.FromResult(global::App.Data.@this.Ok(_answer));
        }
    }

    private static string ForeignDir()
    {
        var p = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(p);
        return p;
    }

    [Test] public async Task Move_OneMissingGrant_ProducesSinglePathAsk()
    {
        // Source out-of-root (no grant), destination in-root (auto-granted).
        // The bundled prompt should mention only the source.
        var app = NewApp(out var root);
        var ch = new CapturingChannel("a");
        app.User.Channels.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(System.IO.Path.Combine(root, "y"), app.User.Context);

        var result = await src.MoveTo(dst);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(1);
        await Assert.That(ch.LastQuestion).Contains(srcFile);
        await Assert.That(ch.LastQuestion).DoesNotContain(dst.Absolute);
        // Move semantics: source must be gone, destination must exist with content.
        await Assert.That(System.IO.File.Exists(srcFile)).IsFalse();
        await Assert.That(System.IO.File.Exists(dst.Absolute)).IsTrue();
        await Assert.That(System.IO.File.ReadAllText(dst.Absolute)).IsEqualTo("data");
    }

    [Test] public async Task Move_BothPathsMissing_ProducesBundledAsk_OneQuestionTwoPaths()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("a");
        app.User.Channels.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.MoveTo(dst);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(1); // single bundled question
        await Assert.That(ch.LastQuestion).Contains(srcFile);
        await Assert.That(ch.LastQuestion).Contains(dstFile);
        // Move semantics: source gone, destination has the data.
        await Assert.That(System.IO.File.Exists(srcFile)).IsFalse();
        await Assert.That(System.IO.File.Exists(dstFile)).IsTrue();
        await Assert.That(System.IO.File.ReadAllText(dstFile)).IsEqualTo("data");
    }

    [Test] public async Task Copy_MirrorsMove_BundledBehavior()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("a");
        app.User.Channels.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.CopyTo(dst);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(1);
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue(); // copy keeps source
        await Assert.That(System.IO.File.Exists(dstFile)).IsTrue();
    }

    [Test] public async Task BundledAsk_AnswerA_StoresBothGrants_SourceReadAndDestWrite()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("a");
        app.User.Channels.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);
        await src.MoveTo(dst);

        // Both grants landed.
        await Assert.That(await app.User.Permission.Find(src, new Verb { Read = new Read() })).IsNotNull();
        await Assert.That(await app.User.Permission.Find(dst, new Verb { Write = new Write() })).IsNotNull();
    }

    private sealed class StatelessChannel : global::App.Channels.Channel.Message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::App.Channels.Channel.ChannelDirection.Bidirectional; }
        public override Task<global::App.Data.@this> WriteCore(global::App.Data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok());
        public override Task<global::App.Data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::App.Data.@this.Ok((object?)null));
    }

    [Test] public async Task Move_BundledAsk_AnswerN_ReturnsPermissionDenied_NoFsMutation()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("n");
        app.User.Channels.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.MoveTo(dst);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsTypeOf<global::App.Errors.PermissionDenied>();
        // No grants stored, no filesystem mutation.
        await Assert.That(await app.User.Permission.Find(src, new Verb { Read = new Read() })).IsNull();
        await Assert.That(await app.User.Permission.Find(dst, new Verb { Write = new Write() })).IsNull();
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue();
        await Assert.That(System.IO.File.Exists(dstFile)).IsFalse();
    }

    [Test] public async Task Copy_BundledAsk_AnswerN_ReturnsPermissionDenied_NoFsMutation()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("n");
        app.User.Channels.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.CopyTo(dst);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsTypeOf<global::App.Errors.PermissionDenied>();
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue();
        await Assert.That(System.IO.File.Exists(dstFile)).IsFalse();
    }

    [Test] public async Task Move_StatelessChannel_BubblesDataAskUnchanged_NoFsMutation()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new StatelessChannel());

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.MoveTo(dst);
        // Stateless: ask result is Exit-typed — Move bubbles it unchanged.
        await Assert.That(result.Type?.Value).IsEqualTo("ask");
        await Assert.That(result.Snapshot).IsNotNull();
        // Nothing mutated on disk.
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue();
        await Assert.That(System.IO.File.Exists(dstFile)).IsFalse();
    }

    [Test] public async Task LegacyV1FsSurface_RoundTripsFile_AlongsideV2()
    {
        // v2 (Path.Operations) is added alongside the v1 fs.File/fs.Directory
        // surface, which non-file-action sites (builder, snapshot, settings)
        // still use. Pin: a round-trip through the v1 surface continues to
        // work — write via fs.File, read via fs.File, assert content.
        var app = NewApp(out var root);
        var rel = "legacy-roundtrip.txt";
        var abs = System.IO.Path.Combine(root, rel);
        await app.FileSystem.File.WriteAllTextAsync(abs, "v1-still-here");
        var roundTripped = await app.FileSystem.File.ReadAllTextAsync(abs);
        await Assert.That(roundTripped).IsEqualTo("v1-still-here");
        // And the v2 surface sees the same bytes.
        var v2 = new Path(abs, app.User.Context);
        var v2Read = await v2.ReadText();
        await Assert.That(v2Read.Success).IsTrue();
        await Assert.That(v2Read.Value).IsEqualTo("v1-still-here");
    }
}
