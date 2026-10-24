using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// Security re-check of X2 (item-doors): edges around the entry a delete or a move acts on. Written against the
/// harness of LinkSpotTests.
public class LinkSpotReCheckProbeTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Asking channel;
    private readonly global::app.actor.context.@this user;

    public LinkSpotReCheckProbeTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ls-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ls-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
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

    // a file moved into a folder LINK that leads out: it lands under where the link leads — asked, never moved unasked
    [Test] public async Task AFileMoved_IntoAFolderLinkLeadingOut_IsAsked_AndNotMovedOnNo()
    {
        if (!Unix) return;
        System.IO.File.WriteAllText(At("in.txt"), "mine");
        System.IO.Directory.CreateSymbolicLink(At("dl"), outside);

        var moved = await user.File(At("in.txt")).MoveTo(user.File(At("dl")), overwrite: true, user);

        await moved.IsFailure();
        await Assert.That(channel.Questions.Count).IsGreaterThan(0);
        await Assert.That(System.IO.File.Exists(At("in.txt"))).IsTrue();
        await Assert.That(System.IO.File.Exists(Out("in.txt"))).IsFalse();
    }

    // a delete's 'always' on the link never answers a read or a write of the file it leads to
    [Test] public async Task ADeletesGrantOnTheLink_NeverAnswersTheTargetsReadOrWrite()
    {
        if (!Unix) return;
        System.IO.File.CreateSymbolicLink(Out("lnk"), Out("secret.txt"));
        channel.Answer = "a";
        await (await user.File(Out("lnk")).Delete(user)).IsSuccess();
        channel.Answer = "n";
        var asks = channel.Questions.Count;

        await (await user.File(Out("secret.txt")).Read(user)).IsFailure();
        await (await user.File(Out("secret.txt")).WriteText("x", user)).IsFailure();

        await Assert.That(channel.Questions.Count).IsEqualTo(asks + 2);
        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");
    }

    // "dl/" is the link "dl": a list of it follows the link — asked on where it lands, never listed unasked
    [Test] public async Task AListOfAFolderLinkWithATrailingSeparator_IsAskedOnWhereItLands()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("dl"), outside);

        var listed = await user.File(At("dl") + "/").List((global::app.type.item.text.@this)"*", (global::app.type.item.@bool.@this)false, user);

        await listed.IsFailure();
        await Assert.That(channel.Questions.Count).IsEqualTo(1);
    }

    // a move of a link to a folder moves the link, never what it leads to: the folder's files stay where they are
    [Test] public async Task AMoveOfAFolderLink_MovesTheLink_AndLeavesTheTargetsFiles()
    {
        if (!Unix) return;
        System.IO.Directory.CreateSymbolicLink(At("dl"), outside);
        channel.Answer = "y";

        await user.File(At("dl")).MoveTo(user.File(At("moved")), overwrite: true, user);

        await Assert.That(System.IO.File.ReadAllText(Out("secret.txt"))).IsEqualTo("outside");
        await Assert.That(System.IO.Directory.Exists(outside)).IsTrue();
    }
}
