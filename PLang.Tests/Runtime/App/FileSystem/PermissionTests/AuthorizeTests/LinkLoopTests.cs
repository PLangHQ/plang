using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// A path whose links loop, or run past the gate's 40, lands nowhere the gate can tell: it is refused, never read as
/// written (in the root as written, while the OS may land elsewhere) and never asked about.
public class LinkLoopTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public LinkLoopTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-loop-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        user = app.actor.list.User.Context;
        channel = new Asking(user.Actor!.Channel, user);
        app.actor.list.User.Channel.Register(channel);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // An input that answers yes to whatever it is asked, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Asking(global::app.channel.list.@this channels, global::app.actor.context.@this ctx) : base(channels) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("y"));
        }
    }

    private string At(params string[] parts) => System.IO.Path.Combine([root, .. parts]);

    private async Task Refused(global::app.data.@this answer)
    {
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("LinkLoop");
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows)] public async Task AReadThroughALinkLoop_IsRefused_NeverAsked()
    {
        System.IO.Directory.CreateSymbolicLink(At("a"), "b");
        System.IO.Directory.CreateSymbolicLink(At("b"), "a");
        await Refused(await user.File(At("a", "x.txt")).Read(user));
    }

    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows)] public async Task AWriteThroughAChainPastFortyLinks_IsRefused_NeverAsked()
    {
        System.IO.Directory.CreateDirectory(At("end"));
        System.IO.Directory.CreateSymbolicLink(At("l41"), "end");
        for (var i = 40; i >= 0; i--) System.IO.Directory.CreateSymbolicLink(At($"l{i}"), $"l{i + 1}");
        await Refused(await user.File(At("l0", "x.txt")).WriteText("x", user));
    }

    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows)] public async Task ACopyOfAFolderHoldingALinkLoop_IsRefused_NothingCopied()
    {
        System.IO.Directory.CreateDirectory(At("dst"));
        System.IO.Directory.CreateSymbolicLink(At("dst", "a"), "b");
        System.IO.Directory.CreateSymbolicLink(At("dst", "b"), "a");
        System.IO.Directory.CreateDirectory(At("src", "a"));
        System.IO.File.WriteAllText(At("src", "a", "f.txt"), "f");
        await Refused(await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user));
    }
}
