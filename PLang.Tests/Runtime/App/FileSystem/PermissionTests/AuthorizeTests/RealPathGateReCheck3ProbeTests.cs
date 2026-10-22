using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Security re-check 3 (item-doors): variants of R2/R4 beyond the first probes — a MOVE into a destination holding a
/// link, a link NESTED in the destination tree, a link loop, a chain past 40 hops, and a link re-created by a copy
/// (it must stay gated at use). Each asserts the SAFE behaviour.
public class RealPathGateReCheck3ProbeTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public RealPathGateReCheck3ProbeTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-rc3-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-rc3-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "outside");
        app = new global::app.@this(root).TestSigning();
        user = app.actor.list.User.Context;
        channel = new Asking(user);
        app.actor.list.User.Channel.Register(channel);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        System.IO.Directory.Delete(outside, true);
    }

    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public string? Question { get; private set; }
        public Asking(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override async Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            Question = (await action.Question!.Value())?.ToString();
            return action.Context.Ok("n");
        }
    }

    private string At(params string[] parts) => System.IO.Path.Combine([root, .. parts]);
    private static bool Unix => !System.OperatingSystem.IsWindows();

    [Test] public async Task AMoveIntoAFolderHoldingALink_NeverWritesWhereTheLinkLeadsOutsideTheRoot()
    {
        if (!Unix) return;
        System.IO.Directory.CreateDirectory(At("src", "d"));
        System.IO.File.WriteAllText(At("src", "d", "f.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst"));
        System.IO.Directory.CreateSymbolicLink(At("dst", "d"), outside);

        await user.File(At("src")).MoveTo(user.File(At("dst")), overwrite: true, user);

        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(outside, "f.txt"))).IsFalse();
    }

    [Test] public async Task ALinkNestedDeepInTheDestination_IsNotWrittenThroughByACopy()
    {
        if (!Unix) return;
        System.IO.Directory.CreateDirectory(At("src", "a", "b"));
        System.IO.File.WriteAllText(At("src", "a", "b", "f.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst", "a"));
        System.IO.Directory.CreateSymbolicLink(At("dst", "a", "b"), outside);

        await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user);

        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(outside, "f.txt"))).IsFalse();
    }

    [Test] public async Task ALinkLoop_IsRefusedWithLinkLoop_NeverAsked_NeverReadAsWritten()
    {
        if (!Unix) return;
        System.IO.File.CreateSymbolicLink(At("a"), At("b"));
        System.IO.File.CreateSymbolicLink(At("b"), At("a"));

        var read = await user.File(At("a")).Read(user);

        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("LinkLoop");
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    [Test] public async Task AChainPast40Hops_ToTheOutside_IsRefused_NotJudgedInRoot()
    {
        if (!Unix) return;
        var target = System.IO.Path.Combine(outside, "secret.txt");
        System.IO.File.CreateSymbolicLink(At("l41"), target);
        for (var i = 40; i >= 0; i--) System.IO.File.CreateSymbolicLink(At("l" + i), At("l" + (i + 1)));

        var read = await user.File(At("l0")).Read(user);

        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Key).IsEqualTo("LinkLoop");
    }

    // a copy re-creates a link as written; the copy is not a read of what it leads to, but a later read through the
    // copy is gated at USE (asked, and answered 'n' here)
    [Test] public async Task ALinkRecreatedByACopy_StaysGatedAtUse()
    {
        if (!Unix) return;
        System.IO.Directory.CreateDirectory(At("src"));
        System.IO.Directory.CreateSymbolicLink(At("src", "l"), outside);
        System.IO.Directory.CreateDirectory(At("dst"));

        await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user);

        await Assert.That(new System.IO.DirectoryInfo(At("dst", "l")).LinkTarget).IsNotNull();
        var before = channel.Count;
        var read = await user.File(At("dst", "l", "secret.txt")).Read(user);
        await Assert.That(read.Success).IsFalse();
        await Assert.That(channel.Count).IsEqualTo(before + 1);
    }
}
