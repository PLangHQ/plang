using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Llm;

/// <summary>
/// Stage 5 — Batch 9. <c>llm/code/OpenAi.cs</c> image attachment denial.
/// Exercises the underlying <c>ReadAsDataUri</c> verb the OpenAI provider's
/// ResolveImage now routes through.
/// </summary>
public class OpenAiImageDenialTests
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
            "plang-imgdeny-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task ImageAttachment_PathOutsideRoot_DeniedAnswer_NotIncludedInRequest()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "img.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 137, 80, 78, 71 });
        var p = new FilePath(outOfRoot, app.User.Context);
        var result = await p.ReadAsDataUri();
        await Assert.That(result.Success).IsFalse();
    }

    [Test] public async Task ImageAttachment_PathInRoot_BytesShipBase64Encoded()
    {
        var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "in.png");
        System.IO.File.WriteAllBytes(file, new byte[] { 137, 80, 78, 71 });
        var p = new FilePath(file, app.User.Context);
        var result = await p.ReadAsDataUri();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!).Contains("base64");
    }
}
