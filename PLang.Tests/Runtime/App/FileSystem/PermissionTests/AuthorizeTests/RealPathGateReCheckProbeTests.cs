using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Security re-check probes (item-doors 315): each asserts the SAFE behaviour, so it is red where the gate has a hole.
/// A user symlink made outside plang (a cloned app ships links) must be judged where the OS lands for it, every verb.
public class RealPathGateReCheckProbeTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public RealPathGateReCheckProbeTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-rc-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-rc-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "outside");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, ".data", "setting"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, ".data", "setting", "data.sqlite"), "the-identity-key");
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

    // An input that refuses whatever it is asked, keeping the question and counting the asks.
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

    // ---- the destination of a tree copy -------------------------------------------------------------------------

    // dst/d is a link (shipped in the app) to a folder outside the root: a copy of src (which has d/f.txt) into dst
    // writes dst/d/f.txt — outside. The top (dst, in the root) is free; an entry that lands outside must not be.
    [Test] public async Task ACopyIntoATree_NeverWritesThroughALinkInIt_ToOutsideTheRoot()
    {
        if (!Unix) return;
        System.IO.Directory.CreateDirectory(At("src", "d"));
        System.IO.File.WriteAllText(At("src", "d", "f.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst"));
        System.IO.Directory.CreateSymbolicLink(At("dst", "d"), outside);

        await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user);

        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(outside, "f.txt"))).IsFalse();
    }

    // dst/x.txt is a link to a file outside: copying src/x.txt over it (overwrite) writes the outside file.
    [Test] public async Task ACopyOverAFileLink_NeverWritesWhereItLeadsOutsideTheRoot()
    {
        if (!Unix) return;
        System.IO.Directory.CreateDirectory(At("src"));
        System.IO.File.WriteAllText(At("src", "x.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst"));
        var target = System.IO.Path.Combine(outside, "target.txt");
        System.IO.File.WriteAllText(target, "original");
        System.IO.File.CreateSymbolicLink(At("dst", "x.txt"), target);

        await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user);

        await Assert.That(System.IO.File.ReadAllText(target)).IsEqualTo("original");
    }

    // A single file copied into dst, where dst/x.txt (shipped) is a link to a file outside: the copy writes the outside
    // file. dst is in the root and free; the entry the copy writes lands outside and is no part of that.
    [Test] public async Task AFileCopiedIntoAFolder_NeverWritesThroughALinkThere_ToOutsideTheRoot()
    {
        if (!Unix) return;
        System.IO.File.WriteAllText(At("x.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst"));
        var target = System.IO.Path.Combine(outside, "target.txt");
        System.IO.File.WriteAllText(target, "original");
        System.IO.File.CreateSymbolicLink(At("dst", "x.txt"), target);

        var copied = await user.File(At("x.txt")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: false, user);

        await copied.IsFailure();
        await Assert.That(System.IO.File.ReadAllText(target)).IsEqualTo("original");
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    // The same for a move: the file never lands where the link in dst leads.
    [Test] public async Task AFileMovedIntoAFolder_NeverWritesThroughALinkThere_ToOutsideTheRoot()
    {
        if (!Unix) return;
        System.IO.File.WriteAllText(At("x.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst"));
        var target = System.IO.Path.Combine(outside, "target.txt");
        System.IO.File.WriteAllText(target, "original");
        System.IO.File.CreateSymbolicLink(At("dst", "x.txt"), target);

        var moved = await user.File(At("x.txt")).MoveTo(user.File(At("dst")), overwrite: true, user);

        await moved.IsFailure();
        await Assert.That(System.IO.File.ReadAllText(target)).IsEqualTo("original");
        await Assert.That(System.IO.File.Exists(At("x.txt"))).IsTrue();
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    // ---- what the question says -----------------------------------------------------------------------------------

    // a link in the root leading out is asked about; the question must name where it lands, not the innocent-looking name
    [Test] public async Task TheQuestion_NamesWhereTheLinkLands_NotJustTheLinksName()
    {
        if (!Unix) return;
        var secret = System.IO.Path.Combine(outside, "secret.txt");
        System.IO.File.CreateSymbolicLink(At("notes.txt"), secret);

        var read = await user.File(At("notes.txt")).Read(user);
        await read.IsFailure();
        await Assert.That(channel.Count).IsEqualTo(1);
        await Assert.That(channel.Question ?? "").Contains(secret);
    }

    // ---- a link made outside plang to the stores / the os folder / outside ---------------------------------------

    [Test] public async Task ALinkToTheStores_ReadThrough_IsRefusedNeverAsked()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("lnk"), At(".data"));
        var read = await user.File(At("lnk", "setting", "data.sqlite")).Read(user);
        await read.IsFailure();
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    [Test] public async Task ALinkToTheStores_ListedWhole_NamesNothingOfTheStores()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("lnk"), At(".data"));
        var listed = await user.File(At("lnk")).List((global::app.type.item.text.@this)"*", (global::app.type.item.@bool.@this)true, user);
        if (listed.Success)
            await Assert.That((await listed.Value())!.Items().Count()).IsEqualTo(0);
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    [Test] public async Task ALinkToTheStores_CopiedOrMoved_CarriesNothingOut()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("lnk"), At(".data"));
        await user.File(At("lnk")).CopyTo(user.File(At("snap")), overwrite: true, subfolder: true, user);
        await user.File(At("lnk")).MoveTo(user.File(At("moved")), overwrite: true, user);
        await Assert.That(System.IO.File.Exists(At("snap", "setting", "data.sqlite"))).IsFalse();
        await Assert.That(System.IO.File.Exists(At("moved", "setting", "data.sqlite"))).IsFalse();
        await Assert.That(System.IO.File.Exists(At(".data", "setting", "data.sqlite"))).IsTrue();
    }

    [Test] public async Task ARecursiveDeleteThroughALinkToTheStores_LeavesTheStoresAlone()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("lnk"), At(".data"));
        await user.File(At("lnk")).Delete(true, user);
        await user.File(At("lnk", "setting")).Delete(true, user);
        await Assert.That(System.IO.File.Exists(At(".data", "setting", "data.sqlite"))).IsTrue();
    }

    // lnk/../.data — the canonical text is judged and is also what the OS opens: the stores
    [Test] public async Task ADotDotAfterALinkComponent_IsJudgedAsTheOpenedPath_NotAsTheLinksParent()
    {
        if (!Unix) return;
        var deep = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(outside, "deep", "deeper")).FullName;
        System.IO.Directory.CreateSymbolicLink(At("lnk"), deep);
        var read = await user.File(At("lnk", "..", ".data", "setting", "data.sqlite")).Read(user);
        await read.IsFailure();
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    [Test] public async Task ALinkToTheOsFolder_WrittenThrough_IsNotWrittenUnasked()
    {
        if (!Unix) return;
        var os = app.filesystem.os.Absolute;
        System.IO.Directory.CreateSymbolicLink(At("oslnk"), os);
        var written = await user.File(At("oslnk", "plang-probe-should-not-exist.txt")).WriteText("x", user);
        await written.IsFailure();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(os, "plang-probe-should-not-exist.txt"))).IsFalse();
    }

    [Test] public async Task ARelativeLinkChainLeavingTheRoot_IsAsked()
    {
        if (!Unix) return;
        var up = "../" + System.IO.Path.GetFileName(outside);
        System.IO.Directory.CreateSymbolicLink(At("l3"), up);
        System.IO.Directory.CreateSymbolicLink(At("l2"), "l3");
        System.IO.Directory.CreateSymbolicLink(At("l1"), "l2");
        var read = await user.File(At("l1", "secret.txt")).Read(user);
        await read.IsFailure();
        await Assert.That(channel.Count).IsEqualTo(1);
    }

    // ---- forms of the stores' folder ----------------------------------------------------------------------------------

    [Test] public async Task CaseTrailingSeparatorBareFolderAndDoubleSlash_AreRefusedNeverAsked()
    {
        var file = At(".data", "setting", "data.sqlite");
        var forms = new[]
        {
            At(".DATA", "setting", "data.sqlite"),
            At(".Data") + System.IO.Path.DirectorySeparatorChar,
            At(".data"),
            At(".data") + System.IO.Path.DirectorySeparatorChar,
            "/" + file,                      // the OS-rooted "//x" form
            At(".data", ".", "setting", "..", "setting", "data.sqlite"),
        };
        foreach (var form in forms)
        {
            var read = await user.File(form).Read(user);
            await Assert.That(read.Success).IsFalse();
        }
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    // ---- links inside a tree ----------------------------------------------------------------------------------------

    [Test] public async Task ALinkToItsOwnFolder_InATree_IsCopiedListedDeleted_WithoutRecursionOrFollowing()
    {
        if (!Unix) return;
        System.IO.Directory.CreateDirectory(At("src"));
        System.IO.File.WriteAllText(At("src", "a.txt"), "a");
        System.IO.Directory.CreateSymbolicLink(At("src", "self"), ".");
        System.IO.Directory.CreateSymbolicLink(At("src", "out"), outside);

        var copied = await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user);
        await copied.IsSuccess();
        await Assert.That(new System.IO.DirectoryInfo(At("dst", "self")).LinkTarget).IsEqualTo(".");

        var listed = await user.File(At("src")).List((global::app.type.item.text.@this)"*", (global::app.type.item.@bool.@this)true, user);
        await listed.IsSuccess();

        await (await user.File(At("src")).Delete(true, user)).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(outside, "secret.txt"))).IsEqualTo("outside");
    }

    [Test] public async Task AMoveOfAnAncestorOfTheStores_IsRefused()
    {
        var moved = await user.File(root).MoveTo(user.File(System.IO.Path.Combine(outside, "root-moved")), overwrite: true, user);
        await moved.IsFailure();
        await Assert.That(System.IO.File.Exists(At(".data", "setting", "data.sqlite"))).IsTrue();
    }

    [Test] public async Task WriteVerbsThroughALinkLeavingTheRoot_AreAsked_NotDone()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("out"), outside);
        await (await user.File(At("out", "new-folder")).Mkdir(user)).IsFailure();
        await (await user.File(At("out", "appended.txt")).Append("x", user)).IsFailure();
        await (await user.File(At("out", "secret.txt")).Delete(user)).IsFailure();
        await Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(outside, "new-folder"))).IsFalse();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(outside, "appended.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(outside, "secret.txt"))).IsTrue();
    }

    [Test] public async Task ACopyOfALinkedOutsideFolder_IsAsked_NotDone()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("out"), outside);
        await user.File(At("out")).CopyTo(user.File(At("grabbed")), overwrite: true, subfolder: true, user);
        await Assert.That(System.IO.File.Exists(At("grabbed", "secret.txt"))).IsFalse();
        await Assert.That(channel.Count).IsGreaterThan(0);
    }
}
