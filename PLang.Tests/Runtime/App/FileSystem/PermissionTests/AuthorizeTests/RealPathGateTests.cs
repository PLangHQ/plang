using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// The gate decides on where the OS lands for a path: each link on the way followed, the app's own folders read the same
/// way. A link in the root leading out is out of the root; a link outside leading in is in it; a grant is on the place.
public class RealPathGateTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public RealPathGateTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-real-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-real-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "outside");
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "a.txt"), "inside");
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

    // An input that refuses whatever it is asked, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Asking(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("n"));
        }
    }

    [Test] public async Task ALinkInTheRootLeadingOut_IsAsked_NeverInRoot()
    {
        if (System.OperatingSystem.IsWindows()) return;
        System.IO.Directory.CreateSymbolicLink(System.IO.Path.Combine(root, "out"), outside);
        var read = await user.File(System.IO.Path.Combine(root, "out", "secret.txt")).Read(user);
        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("PermissionDenied");
        await Assert.That(channel.Count).IsEqualTo(1);
    }

    [Test] public async Task ALinkOutsideLeadingIntoTheRoot_IsInRoot_Unasked()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var into = System.IO.Path.Combine(outside, "in");
        System.IO.Directory.CreateSymbolicLink(into, root);
        var read = await user.File(System.IO.Path.Combine(into, "a.txt")).Read(user);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("inside");
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    // the app opened at a link to its folder: its root is read where the OS lands, so its own files are in it
    [Test] public async Task AnAppRootedAtALink_ReadsItsOwnFiles_Unasked()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var alias = System.IO.Path.Combine(outside, "alias");
        System.IO.Directory.CreateSymbolicLink(alias, root);
        await using var aliased = new global::app.@this(alias).TestSigning();
        var asking = new Asking(aliased.actor.list.User.Context);
        aliased.actor.list.User.Channel.Register(asking);
        var ctx = aliased.actor.list.User.Context;

        var read = await ctx.File(System.IO.Path.Combine(alias, "a.txt")).Read(ctx);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("inside");
        await Assert.That(asking.Count).IsEqualTo(0);
    }

    // a grant is on the place: read through a link in the root to a granted file outside, it is that grant's
    [Test] public async Task AGrantOnThePlace_CoversAReadThroughALinkToIt()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var secret = System.IO.Path.Combine(outside, "secret.txt");
        System.IO.File.CreateSymbolicLink(System.IO.Path.Combine(root, "x.txt"), secret);
        var grant = global::app.type.item.permission.@this.Request(app.actor.list.User, secret, global::app.type.item.permission.Verb.read);
        await app.actor.list.User.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: user), persist: false);

        var read = await user.File(System.IO.Path.Combine(root, "x.txt")).Read(user);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("outside");
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    // a build's overlay holds its own links: one made there is answered there, and the disk is untouched
    [Test] public async Task TheOverlay_HoldsItsOwnLinks()
    {
        var overlay = new global::app.type.item.path.file.filesystem.overlay.@this(app.FileSystem);
        var at = user.File(System.IO.Path.Combine(root, "made"));
        overlay.Link(at, "a.txt");
        await Assert.That(overlay.Link(at)).IsEqualTo("a.txt");
        await Assert.That(app.FileSystem.Link(at)).IsNull();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(root, "made"))).IsFalse();
    }
}
