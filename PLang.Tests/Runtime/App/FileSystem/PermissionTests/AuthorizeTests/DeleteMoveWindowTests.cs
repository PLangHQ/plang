using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Between a verb's check and its act: a recursive delete removes only the entries it checked, children first, each a
/// plain remove — an entry that appeared after the check makes its folder's remove fail, and that is the answer. A move
/// of a folder that holds the stores' folder's place is refused whether the stores are there yet or not.
public class DeleteMoveWindowTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly global::app.actor.context.@this user;

    public DeleteMoveWindowTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-window-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-window-out-" + System.Guid.NewGuid().ToString("N")[..8]);
        app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        user = app.actor.list.User.Context;
        app.actor.list.User.Channel.Register(new Granting(user));
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        if (System.IO.Directory.Exists(outside)) System.IO.Directory.Delete(outside, true);
    }

    // An input that answers "a" to whatever it is asked.
    private sealed class Granting : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public Granting(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
            => Task.FromResult(action.Context.Ok("a"));
    }

    // A disk whose listing doesn't show one file yet: as if it appeared after the check.
    private sealed class Late : global::app.type.item.path.file.filesystem.@this
    {
        private readonly string _late;
        public Late(string root, string late) : base(root) => _late = late;
        public override IEnumerable<global::app.type.item.path.file.@this> List(global::app.type.item.path.file.@this folder, string pattern, bool recursive)
            => base.List(folder, pattern, recursive).Where(entry => entry.Absolute != _late);
    }

    private string At(params string[] parts) => System.IO.Path.Combine([root, .. parts]);

    [Test] public async Task ARecursiveDelete_RemovesOnlyWhatItChecked_ALateEntryIsTheAnswer()
    {
        System.IO.Directory.CreateDirectory(At("gone", "sub"));
        System.IO.File.WriteAllText(At("gone", "a.txt"), "a");
        System.IO.File.WriteAllText(At("gone", "sub", "late.txt"), "late");
        var ctx = new global::app.actor.context.@this(app, app.actor.list.User, parent: user, filesystem: new Late(root, At("gone", "sub", "late.txt")));

        var deleted = await ctx.File(At("gone")).Delete(true, ctx);
        await deleted.IsFailure();
        await Assert.That(deleted.Error!.Key).IsEqualTo("IOError");
        await Assert.That(System.IO.File.ReadAllText(At("gone", "sub", "late.txt"))).IsEqualTo("late");
    }

    // the root's walk shows no stores (as before they are made: reading a grant makes them, so the disk here hides them)
    [Test] public async Task MovingTheRoot_BeforeItsStoresAreMade_IsRefused()
    {
        System.IO.File.WriteAllText(At("a.txt"), "a");
        var ctx = new global::app.actor.context.@this(app, app.actor.list.User, parent: user, filesystem: new Late(root, At(".data")));

        var moved = await ctx.File(root).MoveTo(ctx.File(outside), overwrite: false, ctx);
        await moved.IsFailure();
        await Assert.That(moved.Error!.Key).IsEqualTo("PermissionDenied");
        await Assert.That(System.IO.File.Exists(At("a.txt"))).IsTrue();
        await Assert.That(System.IO.Directory.Exists(outside)).IsFalse();
    }
}
