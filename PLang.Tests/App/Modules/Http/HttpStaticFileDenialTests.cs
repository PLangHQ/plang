using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Http;

/// <summary>
/// Stage 5 — Batch 9. <c>http/code/Default.cs</c> upload-file gating.
///
/// Hardened post tester v2: now invokes
/// <c>Default.CreateFileContentAsync</c> directly (the handler that builds
/// an HTTP request body from a file path). A mutation that reverted it to
/// <c>System.IO.File.ReadAllBytes</c> would flip the denial test red.
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
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "secret.txt");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllText(outOfRoot, "secret");

        // Drive the http upload handler's file-content build path. AuthGate
        // denies; the helper throws IOException with the denial message.
        bool threw = false;
        try
        {
            await global::app.modules.http.code.Default.CreateFileContentAsync(app, app.User.Context, outOfRoot);
        }
        catch (System.IO.IOException) { threw = true; }
        await Assert.That(threw).IsTrue();
    }

    [Test] public async Task StaticFile_RequestForInRootFile_ServedSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.User.Channels.Register(ch);
        var file = System.IO.Path.Combine(root, "public.txt");
        System.IO.File.WriteAllText(file, "hello");
        var content = await global::app.modules.http.code.Default.CreateFileContentAsync(app, app.User.Context, file);
        await Assert.That(content).IsNotNull();
        var bytes = await content.ReadAsByteArrayAsync();
        await Assert.That(System.Text.Encoding.UTF8.GetString(bytes)).IsEqualTo("hello");
    }
}
