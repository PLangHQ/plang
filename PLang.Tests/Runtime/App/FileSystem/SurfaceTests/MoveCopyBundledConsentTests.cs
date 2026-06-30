using Path = global::app.type.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Verb = global::app.type.permission.Verb;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Batch 9: Move/Copy ask each Path its respective verb. Both Ok
/// → operation proceeds. Either returns Data<Ask> → bundled `Ask` with one
/// question string covering both paths. On bundled "a", both grants land.
public class MoveCopyBundledConsentTests
{
    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-mc-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return global::PLang.Tests.TestApp.Plain(root);
    }

    private sealed class CapturingChannel : global::app.channel.@this
    {
        public string LastQuestion { get; private set; } = "";
        public int AskCount { get; private set; }
        private readonly string _answer;
        public CapturingChannel(string answer)
        {
            _answer = answer; Name = "input";
            Direction = global::app.channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            LastQuestion = (action.Question.Peek()?.ToString()) ?? "";
            AskCount++;
            return Task.FromResult(action.Context.Ok(_answer));
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
        app.User.Channel.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(System.IO.Path.Combine(root, "y"), app.User.Context);

        var result = await src.MoveTo(dst, overwrite: true);
        await result.IsSuccess();
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
        app.User.Channel.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.MoveTo(dst, overwrite: true);
        await result.IsSuccess();
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
        app.User.Channel.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.CopyTo(dst, overwrite: true, includeSubfolders: true);
        await result.IsSuccess();
        await Assert.That(ch.AskCount).IsEqualTo(1);
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue(); // copy keeps source
        await Assert.That(System.IO.File.Exists(dstFile)).IsTrue();
    }

    [Test] public async Task BundledAsk_AnswerA_StoresBothGrants_SourceReadAndDestWrite()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("a");
        app.User.Channel.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);
        await src.MoveTo(dst, overwrite: true);

        // Both grants landed.
        await Assert.That(await app.User.Permission.Find(src, global::app.type.permission.Verb.Read)).IsNotNull();
        await Assert.That(await app.User.Permission.Find(dst, global::app.type.permission.Verb.Write)).IsNotNull();
    }

    private sealed class StatelessChannel : global::app.channel.type.message.@this
    {
        public StatelessChannel() { Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));
    }

    [Test] public async Task Move_BundledAsk_AnswerN_ReturnsPermissionDenied_NoFsMutation()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("n");
        app.User.Channel.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.MoveTo(dst, overwrite: true);
        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>();
        // No grants stored, no filesystem mutation.
        await Assert.That(await app.User.Permission.Find(src, global::app.type.permission.Verb.Read)).IsNull();
        await Assert.That(await app.User.Permission.Find(dst, global::app.type.permission.Verb.Write)).IsNull();
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue();
        await Assert.That(System.IO.File.Exists(dstFile)).IsFalse();
    }

    [Test] public async Task Copy_BundledAsk_AnswerN_ReturnsPermissionDenied_NoFsMutation()
    {
        var app = NewApp(out _);
        var ch = new CapturingChannel("n");
        app.User.Channel.Register(ch);

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.CopyTo(dst, overwrite: true, includeSubfolders: true);
        await result.IsFailure();
        await Assert.That(result.Error).IsTypeOf<global::app.error.PermissionDenied>();
        await Assert.That(System.IO.File.Exists(srcFile)).IsTrue();
        await Assert.That(System.IO.File.Exists(dstFile)).IsFalse();
    }

    [Test] public async Task Move_StatelessChannel_BubblesDataAskUnchanged_NoFsMutation()
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new StatelessChannel());

        var srcDir = ForeignDir();
        var srcFile = System.IO.Path.Combine(srcDir, "x");
        System.IO.File.WriteAllText(srcFile, "data");
        var dstDir = ForeignDir();
        var dstFile = System.IO.Path.Combine(dstDir, "y");

        var src = new Path(srcFile, app.User.Context);
        var dst = new Path(dstFile, app.User.Context);

        var result = await src.MoveTo(dst, overwrite: true);
        // Stateless: ask result is Exit-typed — Move bubbles it unchanged.
        await Assert.That(result.Type?.Name).IsEqualTo("ask");
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
        await System.IO.File.WriteAllTextAsync(abs, "v1-still-here");
        var roundTripped = await System.IO.File.ReadAllTextAsync(abs);
        await Assert.That(roundTripped).IsEqualTo("v1-still-here");
        // And the v2 surface sees the same bytes.
        var v2 = new Path(abs, app.User.Context);
        var v2Read = await v2.ReadText();
        await v2Read.IsSuccess();
        await Assert.That((await v2Read.Value())?.ToString()).IsEqualTo("v1-still-here");
    }
}
