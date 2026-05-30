using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Llm;

/// <summary>
/// <c>llm/code/OpenAi.cs</c> image attachment denial. Invokes
/// <c>OpenAi.ResolveImage</c> directly so a mutation that reverted the
/// handler to raw <c>System.IO.File.ReadAllBytes</c> would flip these
/// tests red.
/// </summary>
public class OpenAiImageDenialTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
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
        app.User.Channel.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "img.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 137, 80, 78, 71 });

        // Drive the handler the way the LLM Query flow does — through
        // ResolveImage. Denial → AuthGate fails inside path.ReadAsDataUri
        // → ResolveImage falls through (no bytes shipped). PNG magic bytes
        // (89 50 4E 47) base64-encode to a string starting with "iVBOR" —
        // it MUST NOT appear in the wire content.
        var content = global::app.module.llm.code.OpenAi.ResolveImage(outOfRoot, app, app.User.Context);
        var serialized = System.Text.Json.JsonSerializer.Serialize(content);
        await Assert.That(serialized).DoesNotContain("iVBOR");
    }

    [Test] public async Task ImageAttachment_PathInRoot_BytesShipBase64Encoded()
    {
        var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "in.png");
        System.IO.File.WriteAllBytes(file, new byte[] { 137, 80, 78, 71 });
        // In-root: AuthGate fast-passes inside ReadAsDataUri; bytes ship
        // as a data: URI in the wire payload. Proves the handler routes
        // through the gated verb (mutating to plain System.IO would still
        // produce the same bytes; the proof of *routing* is in the in-root
        // pair with the out-of-root denial test above).
        var content = global::app.module.llm.code.OpenAi.ResolveImage(file, app, app.User.Context);
        var serialized = System.Text.Json.JsonSerializer.Serialize(content);
        await Assert.That(serialized).Contains("iVBOR");
        await Assert.That(serialized).Contains("data:image/png;base64,");
    }
}
