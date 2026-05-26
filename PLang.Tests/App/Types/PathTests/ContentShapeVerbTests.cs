using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 5 — Batch 7. Content-shape verbs (D9a / C9).
/// </summary>
public class ContentShapeVerbTests
{
    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-csv-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    private sealed class CannedChannel : global::app.channels.channel.@this
    {
        private readonly string _answer;
        private readonly System.Collections.Generic.List<string> _prompts = new();
        public System.Collections.Generic.IReadOnlyList<string> Prompts => _prompts;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channels.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> AskCore(global::app.modules.output.ask action, CancellationToken ct = default)
        {
            _prompts.Add(action.Question?.Value ?? "");
            return Task.FromResult(global::app.data.@this.Ok(_answer));
        }
    }

    [Test] public async Task ReadAsBase64_InRoot_ReturnsBase64OfFileBytes()
    {
        var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "data.bin");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        System.IO.File.WriteAllBytes(file, bytes);
        var p = new FilePath(file, app.User.Context);
        var result = await p.ReadAsBase64();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(System.Convert.ToBase64String(bytes));
    }

    [Test] public async Task ReadAsBase64_OutOfRoot_DeniedAnswer_DoesNotReadFile()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "secret.bin");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 42, 43 });
        var p = new FilePath(outOfRoot, app.User.Context);
        var result = await p.ReadAsBase64();
        await Assert.That(result.Success).IsFalse();
        // Differentiate denial from file-not-found / other IO errors.
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test] public async Task ReadAsBase64_GatesUnderReadVerb_NotWriteOrExecute()
    {
        var app = NewApp(out _);
        var canned = new CannedChannel("n");
        app.User.Channels.Register(canned);
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "data.bin");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 1 });
        var p = new FilePath(outOfRoot, app.User.Context);
        await p.ReadAsBase64();
        await Assert.That(canned.Prompts.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(canned.Prompts[0]).Contains("read");
    }

    [Test] public async Task ReadAsDataUri_InRoot_ReturnsDataUriWithCorrectMimePrefix()
    {
        var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "img.png");
        var bytes = new byte[] { 137, 80, 78, 71 };
        System.IO.File.WriteAllBytes(file, bytes);
        var p = new FilePath(file, app.User.Context);
        var result = await p.ReadAsDataUri();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!).StartsWith("data:image/png;base64,");
    }

    [Test] public async Task ReadAsDataUri_OnUnknownExtension_FallsBackToOctetStream()
    {
        var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "blob.weirdext");
        System.IO.File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
        var p = new FilePath(file, app.User.Context);
        var result = await p.ReadAsDataUri();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!).StartsWith("data:application/octet-stream;base64,");
    }

    [Test] public async Task ReadAsDataUri_OutOfRoot_DeniedAnswer_ReturnsDataFail()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "secret.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 1 });
        var p = new FilePath(outOfRoot, app.User.Context);
        var result = await p.ReadAsDataUri();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("PermissionDenied");
    }
}
