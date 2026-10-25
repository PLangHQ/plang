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
        private readonly global::app.actor.context.@this _ctx;
        public CannedChannel(global::app.channel.list.@this channels, string answer, global::app.actor.context.@this ctx) : base(channels) { _answer = answer; _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(_ctx.Ok(_answer));
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-imgdeny-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
    }

    [Test] public async Task ImageAttachment_PathOutsideRoot_DeniedAnswer_IsTheAnswer_NoBytes()
    {
        await using var app = NewApp(out _);
        app.actor.list.User.Channel.Register(new CannedChannel(app.actor.list.User.Channel, "n", app.actor.list.User.Context));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "img.png");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllBytes(outOfRoot, new byte[] { 137, 80, 78, 71 });
        // the file in plang's OS-rooted form, so the read reaches the gate on both OSes: //x on Unix (a single / is the
        // app's root), C:\x on Windows
        var form = System.OperatingSystem.IsWindows() ? outOfRoot : "/" + outOfRoot;

        // Drive the handler the way the LLM Query flow does — through ResolveImage. The read asks the gate, the
        // answer is "n": the denial is ResolveImage's answer, carried whole, and nothing of the file ships.
        var thrown = await Assert.ThrowsAsync<global::app.error.AppException>(async () =>
            await global::app.module.llm.code.OpenAi.ResolveImage(form, app, app.actor.list.User.Context));
        await Assert.That(thrown!.Error.Key).IsEqualTo("PermissionDenied");
    }

    [Test] public async Task ImageAttachment_PathInRoot_BytesShipBase64Encoded()
    {
        await using var app = NewApp(out var root);
        var file = System.IO.Path.Combine(root, "in.png");
        System.IO.File.WriteAllBytes(file, new byte[] { 137, 80, 78, 71 });
        // In-root: AuthGate fast-passes inside ReadAsDataUri; bytes ship
        // as a data: URI in the wire payload. Proves the handler routes
        // through the gated verb (mutating to plain System.IO would still
        // produce the same bytes; the proof of *routing* is in the in-root
        // pair with the out-of-root denial test above).
        var content = await global::app.module.llm.code.OpenAi.ResolveImage(file, app, app.actor.list.User.Context);
        var serialized = System.Text.Json.JsonSerializer.Serialize(content);
        await Assert.That(serialized).Contains("iVBOR");
        await Assert.That(serialized).Contains("data:image/png;base64,");
    }
}
