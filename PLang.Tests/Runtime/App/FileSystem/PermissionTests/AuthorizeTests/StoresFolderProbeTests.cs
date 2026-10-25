using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

// security re-check probes (item-doors, I1) — uncommitted
public class StoresFolderProbeTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string key;
    private readonly global::app.@this app;
    private int asked;

    public StoresFolderProbeTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-probe-" + System.Guid.NewGuid().ToString("N")[..8]);
        key = System.IO.Path.Combine(root, ".data", "setting", "data.sqlite");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(key)!);
        System.IO.File.WriteAllText(key, "SECRET-KEY");
        app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        app.actor.list.User.Channel.Register(new Asking(app.actor.list.User.Context, () => asked++));
    }
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx; private readonly System.Action _n;
        public Asking(global::app.actor.context.@this ctx, System.Action n)
        { _ctx = ctx; _n = n; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        { _n(); return Task.FromResult(action.Context.Ok("y")); }
    }

    // P1: copy the whole root (in-root, authorized) — does .data land readable in /snap?
    [Test] public async Task P1_CopyingTheRoot_DoesNotCarryTheStores()
    {
        var user = app.actor.list.User.Context;
        var snap = System.IO.Path.Combine(root, "snap");
        await user.File(root).CopyTo(user.File(snap), overwrite: true, subfolder: true, user);
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(snap, ".data", "setting", "data.sqlite"))).IsFalse();
    }

    // P2: the OS-rooted "//" form of the same file — asked rather than refused?
    [Test] public async Task P2_DoubleSlashForm_IsRefusedNotAsked()
    {
        var user = app.actor.list.User.Context;
        var r = await user.File("/" + key).Read(user);   // "//home/.../.data/setting/data.sqlite"
        await Assert.That(asked).IsEqualTo(0);
        await Assert.That(r.Success).IsFalse();
    }

    // P3: a symlink inside the root that leads into .data
    [Test] public async Task P3_SymlinkIntoTheStores_IsRefused()
    {
        System.IO.File.CreateSymbolicLink(System.IO.Path.Combine(root, "lnk"), System.IO.Path.Combine(root, ".data"));
        var user = app.actor.list.User.Context;
        var r = await user.File(System.IO.Path.Combine(root, "lnk", "setting", "data.sqlite")).Read(user);
        await Assert.That(r.Success).IsFalse();
    }

    // P4: the bare .data folder for the system (the store open does parent.Mkdir)
    [Test] public async Task P4_SystemMkdirOfTheBareStoresFolder_IsAllowed()
    {
        var sys = app.actor.list.System.Context;
        var r = await sys.File(System.IO.Path.Combine(root, ".data")).Mkdir(sys);
        await Assert.That(r.Success).IsTrue();
    }

    // P5: bare folder for the user is refused, listing the root as user (names only)
    [Test] public async Task P5_ListingTheRoot_DoesNotNameTheStoresFiles()
    {
        var user = app.actor.list.User.Context;
        var l = await user.File(root).List("*", true, user);
        var names = string.Join("|", (await l.Value())!.Items(user).Select(p => p.ToString()));
        await Assert.That(names.Contains("data.sqlite")).IsFalse();
    }
}
