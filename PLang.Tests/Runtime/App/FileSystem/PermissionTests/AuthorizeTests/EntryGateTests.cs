using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// A copy, move or recursive delete asks the gate for every entry it touches, both ends, before it touches any: the top's
/// ask covers what would be asked, and an entry the gate refuses (the stores' folder) refuses the verb whole — nothing
/// copied, moved or deleted.
public class EntryGateTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string key;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public EntryGateTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-entry-" + System.Guid.NewGuid().ToString("N")[..8]);
        key = System.IO.Path.Combine(root, ".data", "setting", "data.sqlite");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(key)!);
        System.IO.File.WriteAllText(key, "the identity's private key");
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "a.txt"), "a");
        app = new global::app.@this(root).TestSigning();
        user = app.actor.list.User.Context;
        channel = new Asking(user);
        app.actor.list.User.Channel.Register(channel);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // An input that answers "a" to whatever it is asked, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Asking(global::app.actor.context.@this ctx)
        {
            _ctx = ctx; Name = "input";
            Direction = global::app.channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("a"));
        }
    }

    private string At(params string[] parts) => System.IO.Path.Combine([root, .. parts]);

    private static async Task Refused(global::app.data.@this answer)
    {
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("PermissionDenied");
    }

    private async Task StoresUntouched()
        => await Assert.That(System.IO.File.ReadAllText(key)).IsEqualTo("the identity's private key");

    [Test] public async Task MovingTheRoot_IsRefused_NothingMoved()
    {
        await Refused(await user.File(root).MoveTo(user.File(At("x")), overwrite: false, user));
        await Assert.That(System.IO.File.Exists(At("a.txt"))).IsTrue();
        await Assert.That(System.IO.Directory.Exists(At("x"))).IsFalse();
        await StoresUntouched();
    }

    [Test] public async Task DeletingTheRootRecursively_IsRefused_NothingDeleted()
    {
        await Refused(await user.File(root).Delete(true, user));
        await Assert.That(System.IO.File.Exists(At("a.txt"))).IsTrue();
        await StoresUntouched();
    }

    [Test] public async Task CopyingIntoTheRoot_AFolderHoldingItsOwnStores_IsRefused_TheStoresUntouched()
    {
        var forged = At("pack", ".data", "setting", "data.sqlite");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(forged)!);
        System.IO.File.WriteAllText(forged, "a forged key");
        System.IO.File.WriteAllText(At("pack", "b.txt"), "b");

        await Refused(await user.File(At("pack")).CopyTo(user.File(root), overwrite: true, subfolder: true, user));
        await StoresUntouched();
        await Assert.That(System.IO.File.Exists(At("b.txt"))).IsFalse();
    }

    [Test] public async Task CopyingTheRoot_IsRefused_NothingAtTheDestination()
    {
        await Refused(await user.File(root).CopyTo(user.File(At("snap")), overwrite: true, subfolder: true, user));
        await Assert.That(System.IO.Directory.Exists(At("snap"))).IsFalse();
    }

    [Test] public async Task AFolderFromOutsideTheRoot_CopiedAfterOneAnswer_IsAskedOnce()
    {
        var foreign = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(foreign, "sub"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(foreign, "y.txt"), "y");
        System.IO.File.WriteAllText(System.IO.Path.Combine(foreign, "sub", "x.txt"), "x");

        await (await user.File(foreign).CopyTo(user.File(At("in")), overwrite: true, subfolder: true, user)).IsSuccess();
        await Assert.That(channel.Count).IsEqualTo(1);
        await Assert.That(System.IO.File.ReadAllText(At("in", "sub", "x.txt"))).IsEqualTo("x");
        await Assert.That(System.IO.File.ReadAllText(At("in", "y.txt"))).IsEqualTo("y");
    }

    [Test] public async Task AnInRootFolderCopy_StillWorks_Unasked()
    {
        System.IO.Directory.CreateDirectory(At("src", "sub"));
        System.IO.File.WriteAllText(At("src", "c.txt"), "c");
        System.IO.File.WriteAllText(At("src", "sub", "d.txt"), "d");

        await (await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user)).IsSuccess();
        await Assert.That(channel.Count).IsEqualTo(0);
        await Assert.That(System.IO.File.ReadAllText(At("dst", "sub", "d.txt"))).IsEqualTo("d");
        await Assert.That(System.IO.File.ReadAllText(At("dst", "c.txt"))).IsEqualTo("c");
    }
}
