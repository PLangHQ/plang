using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// A link inside a folder a verb walks is a link: a copy makes it again with its target as written (cp -P), never reading
/// or descending through it; a delete removes the link, never what it leads to. A read through it later is the gate's.
public class LinkEntryTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly global::app.actor.context.@this user;

    public LinkEntryTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-linkentry-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-linkentry-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "outside");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "src"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "src", "a.txt"), "a");
        app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        user = app.actor.list.User.Context;
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        System.IO.Directory.Delete(outside, true);
    }

    private string At(params string[] parts) => System.IO.Path.Combine([root, .. parts]);

    private async Task<global::app.data.@this> Copy()
        => await user.File(At("src")).CopyTo(user.File(At("dst")), overwrite: true, subfolder: true, user);

    [Test] public async Task AFileLink_IsCopiedAsALink_ItsTargetNeverRead()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var target = System.IO.Path.Combine(outside, "secret.txt");
        System.IO.File.CreateSymbolicLink(At("src", "l.txt"), target);

        await (await Copy()).IsSuccess();
        await Assert.That(new System.IO.FileInfo(At("dst", "l.txt")).LinkTarget).IsEqualTo(target);
        await Assert.That(System.IO.File.ReadAllText(At("dst", "a.txt"))).IsEqualTo("a");
    }

    [Test] public async Task AFolderLink_IsCopiedAsALink_NeverDescended()
    {
        if (System.OperatingSystem.IsWindows()) return;
        System.IO.Directory.CreateSymbolicLink(At("src", "d"), outside);

        await (await Copy()).IsSuccess();
        await Assert.That(new System.IO.DirectoryInfo(At("dst", "d")).LinkTarget).IsEqualTo(outside);
    }

    [Test] public async Task ARecursiveDelete_RemovesTheLink_NeverWhatItLeadsTo()
    {
        if (System.OperatingSystem.IsWindows()) return;
        System.IO.Directory.CreateSymbolicLink(At("src", "d"), outside);

        await (await user.File(At("src")).Delete(true, user)).IsSuccess();
        await Assert.That(System.IO.Directory.Exists(At("src"))).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(outside, "secret.txt"))).IsEqualTo("outside");
    }
}
