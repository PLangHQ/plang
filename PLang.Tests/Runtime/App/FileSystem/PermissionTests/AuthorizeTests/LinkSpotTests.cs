using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Each verb is judged where the OS acts for it: an open follows the last link; an unlink or a rename (a delete, a move's
/// ends) acts on the entry itself — its folder's landing and its own name.
public class LinkSpotTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public LinkSpotTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-le-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-le-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "secret.txt"), "outside");
        app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        user = app.actor.list.User.Context;
        channel = new Asking(user);
        app.actor.list.User.Channel.Register(channel);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        System.IO.Directory.Delete(outside, true);
        if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
    }

    // An input answering what it is told to (n by default), keeping every question.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public List<string> Questions { get; } = new();
        public string Answer { get; set; } = "n";
        public Asking(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override async Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Questions.Add((await action.Question!.Value())?.ToString() ?? "");
            return action.Context.Ok(Answer);
        }
    }

    private string At(params string[] parts) => System.IO.Path.Combine([root, .. parts]);
    private string Out(params string[] parts) => System.IO.Path.Combine([outside, .. parts]);
    private static bool Unix => !System.OperatingSystem.IsWindows();
    private static bool IsLink(string path) => new System.IO.FileInfo(path).LinkTarget != null;

    // ---- delete: the entry -------------------------------------------------------------------------------------------

    // /outside/lnk -> <root>/in.txt: deleting it unlinks an entry outside the root — asked, never judged where it leads
    [Test] public async Task AnOutsideLinkIntoTheRoot_Deleted_IsAsked()
    {
        if (!Unix) return;
        System.IO.File.WriteAllText(At("in.txt"), "in");
        System.IO.File.CreateSymbolicLink(Out("lnk"), At("in.txt"));

        var deleted = await user.File(Out("lnk")).Delete(user);

        await deleted.IsFailure();
        await Assert.That(channel.Questions.Count).IsEqualTo(1);
        await Assert.That(IsLink(Out("lnk"))).IsTrue();
    }

    // <root>/lnk -> /outside/secret.txt: deleting it unlinks an entry in the root — free, the file it leads to untouched
    [Test] public async Task AnInRootLinkToOutside_Deleted_IsFree_AndItsTargetKept()
    {
        if (!Unix) return;
        System.IO.File.CreateSymbolicLink(At("lnk"), Out("secret.txt"));

        var deleted = await user.File(At("lnk")).Delete(user);

        await deleted.IsSuccess();
        await Assert.That(channel.Questions.Count).IsEqualTo(0);
        await Assert.That(System.IO.Path.Exists(At("lnk")) || IsLink(At("lnk"))).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");
    }

    // a delete's yes is on the link — the place the question names — and for delete alone: it never answers a read of it
    [Test] public async Task ADeletesGrant_IsOnTheLink_AndNeverAnswersAReadOfIt()
    {
        if (!Unix) return;
        System.IO.File.CreateSymbolicLink(Out("lnk"), Out("secret.txt"));
        channel.Answer = "a";
        await (await user.File(Out("lnk")).Delete(user)).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");

        // the link made again: a read of it is asked, on where it lands
        System.IO.File.CreateSymbolicLink(Out("lnk"), Out("secret.txt"));
        channel.Answer = "n";
        await (await user.File(Out("lnk")).Read(user)).IsFailure();

        await Assert.That(channel.Questions.Count).IsEqualTo(2);
        await Assert.That(channel.Questions[0]).Contains("delete " + Out("lnk") + "?");
    }

    // <root>/dl -> /outside (a folder): deleting it unlinks it — never what is under where it leads
    [Test] public async Task AFolderLink_Deleted_IsUnlinked_NeverRecursedThrough()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("dl"), outside);

        var deleted = await user.File(At("dl")).Delete(true, user);

        await deleted.IsSuccess();
        await Assert.That(channel.Questions.Count).IsEqualTo(0);
        await Assert.That(IsLink(At("dl"))).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");
    }

    // "dl/" is the link "dl" — judged and done as it, the trailing separator no different place
    [Test] public async Task AFolderLinkWithATrailingSeparator_IsJudgedAndDeletedAsTheLink()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("dl"), outside);

        var deleted = await user.File(At("dl") + "/").Delete(true, user);

        await deleted.IsSuccess();
        await Assert.That(channel.Questions.Count).IsEqualTo(0);
        await Assert.That(IsLink(At("dl"))).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");
    }

    // A Windows junction to a folder outside, deleted: the junction goes, what it leads to stays. Runs on Windows only.
    [Test] public async Task AJunction_Deleted_IsUnlinked_NeverRecursedThrough()
    {
        if (!System.OperatingSystem.IsWindows()) return;
        var made = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{At("j")}\" \"{outside}\"")
            { UseShellExecute = false, CreateNoWindow = true })!;
        await made.WaitForExitAsync();
        await Assert.That(made.ExitCode).IsEqualTo(0);

        var deleted = await user.File(At("j")).Delete(true, user);

        await deleted.IsSuccess();
        await Assert.That(System.IO.Directory.Exists(At("j"))).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");
    }

    // ---- move: a rename's ends ----------------------------------------------------------------------------------------

    // /outside/lnk -> <root>/in.txt, moved into the root: the rename takes an entry outside away — asked
    [Test] public async Task AnOutsideLinkIntoTheRoot_Moved_IsAsked()
    {
        if (!Unix) return;
        System.IO.File.WriteAllText(At("in.txt"), "in");
        System.IO.File.CreateSymbolicLink(Out("lnk"), At("in.txt"));

        var moved = await user.File(Out("lnk")).MoveTo(user.File(At("moved")), overwrite: true, user);

        await moved.IsFailure();
        await Assert.That(channel.Questions.Count).IsEqualTo(1);
        await Assert.That(IsLink(Out("lnk"))).IsTrue();
    }

    // a move over <root>/dst/x.txt, a link leading out: the rename replaces the link, never writes where it leads
    [Test] public async Task AMoveOverAnInRootLinkLeadingOut_ReplacesTheLink()
    {
        if (!Unix) return;
        System.IO.File.WriteAllText(At("x.txt"), "planted");
        System.IO.Directory.CreateDirectory(At("dst"));
        System.IO.File.WriteAllText(Out("target.txt"), "original");
        System.IO.File.CreateSymbolicLink(At("dst", "x.txt"), Out("target.txt"));

        var moved = await user.File(At("x.txt")).MoveTo(user.File(At("dst", "x.txt")), overwrite: true, user);

        await moved.IsSuccess();
        await Assert.That(channel.Questions.Count).IsEqualTo(0);
        await Assert.That(IsLink(At("dst", "x.txt"))).IsFalse();
        await Assert.That(System.IO.File.ReadAllText(At("dst", "x.txt"))).IsEqualTo("planted");
        await Assert.That(System.IO.File.ReadAllText(Out("target.txt"))).IsEqualTo("original");
    }

    // a read grant alone never removes a file by moving it: the move's source is asked delete too
    [Test] public async Task AMovesSource_WithOnlyAReadGrant_IsAskedDelete()
    {
        var grant = new global::app.type.item.permission.@this("User", Out("secret.txt"),
            new HashSet<global::app.type.item.permission.Verb> { global::app.type.item.permission.Verb.read },
            global::app.type.item.permission.Match.exact, app.type.list[typeof(global::app.type.item.permission.@this)]);
        await user.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: user), persist: false);

        var moved = await user.File(Out("secret.txt")).MoveTo(user.File(At("taken.txt")), overwrite: true, user);

        await moved.IsFailure();
        await Assert.That(channel.Questions.Count).IsEqualTo(1);
        await Assert.That(channel.Questions[0]).Contains("delete " + Out("secret.txt"));
        await Assert.That(channel.Questions[0]).DoesNotContain("read ");
        await Assert.That(System.IO.File.Exists(Out("secret.txt"))).IsTrue();
    }
}
