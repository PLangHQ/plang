using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Ui;

/// <summary>
/// Stage 5 — Batch 9. Fluid template-read gating. Exercises the underlying
/// path.ReadText that PlangFileInfo.CreateReadStream now routes through.
/// </summary>
public class FluidIncludeDenialTests
{
    private sealed class CannedChannel : global::app.channels.channel.@this
    {
        public int AskCount;
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channels.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> AskCore(global::app.modules.output.ask action, CancellationToken ct = default)
        {
            System.Threading.Interlocked.Increment(ref AskCount);
            return Task.FromResult(global::app.data.@this.Ok(_answer));
        }
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fluid-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task FluidInclude_TemplateOutsideRoot_DeniedByAuthGate()
    {
        var app = NewApp(out _);
        app.User.Channels.Register(new CannedChannel("n"));
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8], "tpl.liquid");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outOfRoot)!);
        System.IO.File.WriteAllText(outOfRoot, "{{ secret }}");
        var p = new FilePath(outOfRoot, app.User.Context);
        var read = await p.ReadText();
        await Assert.That(read.Success).IsFalse();
    }

    [Test] public async Task FluidInclude_InRootTemplate_RendersSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.User.Channels.Register(ch);
        var file = System.IO.Path.Combine(root, "tpl.liquid");
        System.IO.File.WriteAllText(file, "hello {{ name }}");
        var p = new FilePath(file, app.User.Context);
        var read = await p.ReadText();
        await Assert.That(read.Success).IsTrue();
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }
}
