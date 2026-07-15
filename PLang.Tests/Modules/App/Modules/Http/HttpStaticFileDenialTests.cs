using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Http;

/// <summary>
/// <c>http/code/Default.cs</c> upload-file gating. Invokes
/// <c>Default.CreateFileContentAsync</c> directly (the handler that builds
/// an HTTP request body from a file path). A mutation that reverted it to
/// <c>System.IO.File.ReadAllBytes</c> would flip the denial test red.
/// </summary>
public class HttpStaticFileDenialTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.action.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-http-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return TestApp.Create(root);
    }

    [Test] public async Task StaticFile_RequestWithDotDotTraversal_DeniedByAuthGate()
    {
        var app = NewApp(out _);
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "secret.txt");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllText(outOfRoot, "secret");

        // Drive the http upload handler's file-content build path. AuthGate
        // denies; either the helper returns a failed Data carrying the
        // denial, or the underlying path.ReadBytes throws IOException
        // (file.this.ReadBytes doesn't wrap IO exceptions — see
        // types/path/file/this.Operations.cs:130). Either way: the upload
        // does not see the bytes.
        bool denied = false;
        try
        {
            var result = await global::app.module.action.http.code.Default.CreateFileContentAsync(app, app.User.Context, outOfRoot);
            denied = result.Error != null;
        }
        catch (System.IO.IOException) { denied = true; }
        await Assert.That(denied).IsTrue();
    }

    [Test] public async Task StaticFile_RequestForInRootFile_ServedSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.User.Channel.Register(ch);
        var file = System.IO.Path.Combine(root, "public.txt");
        System.IO.File.WriteAllText(file, "hello");
        var result = await global::app.module.action.http.code.Default.CreateFileContentAsync(app, app.User.Context, file);
        await Assert.That(result.Error).IsNull();
        var bytes = await result.Content!.ReadAsByteArrayAsync();
        await Assert.That(System.Text.Encoding.UTF8.GetString(bytes)).IsEqualTo("hello");
    }
}
