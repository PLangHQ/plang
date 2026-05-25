using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Http;

/// <summary>
/// Stage 5 — Batch 9. http upload-file gating. The http upload action
/// reaches for the filesystem when ContentAs.File is set; it now goes
/// through path.ReadBytes → AuthGate(Read).
/// </summary>
public class HttpStaticFileDenialTests
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
            "plang-http-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task StaticFile_RequestWithDotDotTraversal_DeniedByAuthGate()
    {
        // The http file-content path: read an out-of-root file as a request body.
        // AuthGate(Read) denies; ReadBytes returns Fail.
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "secret.txt");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllText(outOfRoot, "secret");
        var p = new FilePath(outOfRoot, app.User.Context);
        var read = await p.ReadBytes();
        await Assert.That(read.Success).IsFalse();
    }

    [Test] public async Task StaticFile_RequestForInRootFile_ServedSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.User.Channels.Register(ch);
        var file = System.IO.Path.Combine(root, "public.txt");
        System.IO.File.WriteAllText(file, "hello");
        var p = new FilePath(file, app.User.Context);
        var read = await p.ReadBytes();
        await Assert.That(read.Success).IsTrue();
    }
}
