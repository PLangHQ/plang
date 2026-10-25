using Verb = global::app.type.item.permission.Verb;
using Grant = global::app.type.item.permission.@this;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Security probes (decision 49, fast-apps, S-A): a sub app is made in its maker's folder or under it. The rule is
/// <c>filesystem.root.IsUnder(App.filesystem.root)</c> on the LANDED roots (path text canonicalized — `..` folded —
/// then the real path, links resolved); containment is `Absolute.StartsWith(root + sep)` or equal, with the root's
/// own comparison (Ordinal off Windows). Each test asserts the SAFE behaviour: red is a hole.
public class SubAppContainmentProbeTests : System.IAsyncDisposable
{
    private readonly string baseDir;
    private readonly string maker;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly List<global::app.@this> born = new();

    public SubAppContainmentProbeTests()
    {
        baseDir = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ct-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        maker = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "maker")).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "outside", "deep")).FullName;
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker, "inside"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "maker2"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "maker.d"));
        app = new global::app.@this(maker, global::PLang.Tests.Parent.Actor).TestSigning();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        foreach (var b in born) await b.DisposeAsync();
        await app.DisposeAsync();
        try { System.IO.Directory.Delete(baseDir, true); } catch { /* links to folders are removed alone */ }
    }

    private string M(params string[] parts) => System.IO.Path.Combine([maker, .. parts]);

    // a sub app made by the maker's User at `path`: born, or refused with its error
    private (global::app.@this? sub, global::app.error.AppException? refused) Birth(string path)
    {
        try
        {
            var sub = new global::app.@this(path, app.actor.list.User);
            born.Add(sub);
            return (sub, null);
        }
        catch (global::app.error.AppException ex) { return (null, ex); }
    }

    private bool UnderMaker(global::app.@this sub) => sub.filesystem.root.IsUnder(app.filesystem.root).Value;

    // the safe invariant for any path: whatever is born lands under the maker; whatever lands elsewhere is refused
    private async Task AssertSafe(string path, bool? expectBorn = null)
    {
        var (sub, refused) = Birth(path);
        if (sub != null) await Assert.That(UnderMaker(sub)).IsTrue();
        else await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker");
        if (expectBorn is { } want) await Assert.That(sub != null).IsEqualTo(want);
    }

    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public List<string> Questions { get; } = new();
        public Asking(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override async Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Questions.Add((await action.Question!.Value())?.ToString() ?? "");
            return action.Context.Ok("n");
        }
    }

    // every actor of the app answers 'n' to a question (never reads a console)
    private static Asking Mute(global::app.actor.@this actor)
    {
        var ch = new Asking(actor.Context);
        actor.Channel.Register(ch);
        return ch;
    }

    private static async Task<bool> Passes(global::app.actor.context.@this ctx, string path, Verb verb)
        => (await ctx.File(path).Authorize(verb, ctx)).Success;

    // ── S1 `..` ────────────────────────────────────────────────────────────────

    [Test]
    [Arguments("sub/..", true)]            // == the maker
    [Arguments("./sub", true)]
    [Arguments("sub/", true)]
    [Arguments("sub/../sub2", true)]
    [Arguments("sub/../..", false)]        // the maker's parent
    [Arguments("sub/../../x", false)]
    [Arguments("../maker2", false)]        // a sibling
    [Arguments("../maker.d", false)]
    public async Task S1_DotDotInTheSubAppsRoot_IsFoldedBeforeTheContainmentIsAsked(string tail, bool accepted)
        => await AssertSafe(maker + "/" + tail, accepted);

    [Test]
    [Arguments("\\sub\\..")]
    [Arguments("\\sub\\..\\..")]
    [Arguments("\\..\\maker2")]
    [Arguments("/sub\\..\\..")]
    public async Task S1_MixedBackslashForms_NeverBeBornOutsideTheMaker(string tail)
        => await AssertSafe(maker + tail);

    // ── S2 symlinks ────────────────────────────────────────────────────────────

    private void Link(string at, string target) => System.IO.Directory.CreateSymbolicLink(at, target);

    [Test]
    public async Task S2_ALinkInTheMakerLeadingOutside_IsRefused_ExistingOrNot()
    {
        Link(M("lnk"), outside);
        await AssertSafe(M("lnk"), false);
        await AssertSafe(M("lnk", "new"), false);               // the deepest existing part lands outside
        await AssertSafe(M("lnk", "deep"), false);
    }

    [Test]
    public async Task S2_ARelativeLinkLeavingTheMaker_IsRefused()
    {
        Link(M("rel"), "../outside");
        await AssertSafe(M("rel"), false);
    }

    [Test]
    public async Task S2_ATwoHopChainLeavingTheMaker_IsRefused()
    {
        Link(M("c2"), outside);
        Link(M("c1"), M("c2"));
        await AssertSafe(M("c1"), false);
    }

    [Test]
    public async Task S2_ALinkToTheMakersParent_IsRefused()
    {
        Link(M("up"), baseDir);
        await AssertSafe(M("up"), false);
        await AssertSafe(M("up", "maker"), true);     // through the parent back into the maker: the landed folder IS the maker's
    }

    [Test]
    public async Task S2_ALinkToAPrefixConfusableSibling_IsRefused()
    {
        Link(M("pc"), System.IO.Path.Combine(baseDir, "maker2"));
        await AssertSafe(M("pc"), false);
    }

    [Test]
    public async Task S2_ALinkInsideTheMakerLeadingToAnotherPlaceInsideIt_IsTaken()
    {
        Link(M("lnk2"), M("inside"));
        await AssertSafe(M("lnk2"), true);
    }

    [Test]
    public async Task S2_TheMakerReachedThroughALinkToItsParent_LandsAsTheMakerItself()
    {
        var viaLink = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ct-link-" + System.Guid.NewGuid().ToString("N")[..8]);
        Link(viaLink, baseDir);
        try { await AssertSafe(System.IO.Path.Combine(viaLink, "maker", "sub")); }
        finally { System.IO.Directory.Delete(viaLink); }
    }

    // ── S3 case ────────────────────────────────────────────────────────────────

    [Test]
    public async Task S3_TheContainmentComparison_IsTheRootsOwn_SoADifferentCaseIsAnotherFolderOffWindows()
    {
        var comparison = (System.StringComparison)typeof(global::app.type.item.path.@this)
            .GetProperty("RootComparison", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        await Assert.That(comparison).IsEqualTo(System.OperatingSystem.IsWindows() ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal);

        // on a case-sensitive disk MAKER is not maker: refused; a different case in the CHILD is just another name under it
        var upper = System.IO.Path.Combine(baseDir, "MAKER");
        if (System.IO.Directory.Exists(upper))     // a case-insensitive disk: it is the maker's own folder
            await AssertSafe(upper);
        else
            await AssertSafe(upper, false);
        await AssertSafe(M("SUB"), true);
    }

    // ── S4 a sub app rooted at the maker's own stores ──────────────────────────

    [Test]
    [Arguments(".data")]
    [Arguments(".data/setting")]
    public async Task S4_ASubAppRootedAtTheMakersStores_DoesNotHandItsUserTheStoresFiles(string tail)
    {
        var folder = M(tail.Split('/'));
        System.IO.Directory.CreateDirectory(folder);
        var key = System.IO.Path.Combine(folder, "identity.key");
        System.IO.File.WriteAllText(key, "secret");

        // baseline: the maker's own User is refused on its stores
        Mute(app.actor.list.User);
        await Assert.That(await Passes(app.actor.list.User.Context, key, Verb.read)).IsFalse();

        // the sub app: refused at birth, or its User refused on the files of that root (the safe outcomes)
        var (sub, refused) = Birth(folder);
        if (sub == null) { await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker"); return; }
        Mute(sub.actor.list.User);
        await Assert.That(await Passes(sub.actor.list.User.Context, key, Verb.read)).IsFalse();
    }

    // ── S5 prefix confusion and a root maker ───────────────────────────────────

    [Test]
    public async Task S5_APrefixOfTheMakersName_IsAnotherFolder()
    {
        await AssertSafe(System.IO.Path.Combine(baseDir, "maker2"), false);
        await AssertSafe(System.IO.Path.Combine(baseDir, "maker.d"), false);
        await AssertSafe(System.IO.Path.Combine(baseDir, "maker-x"), false);
    }

    [Test]
    public async Task S5_ARootMaker_TakesAnyFolder_WithoutADoubledSeparator()
    {
        await using var rooted = new global::app.@this("/", global::PLang.Tests.Parent.Actor);
        await using var any = new global::app.@this(System.IO.Path.Combine(baseDir, "anything"), rooted.actor.list.User);
        await Assert.That(any.filesystem.root.Absolute).IsEqualTo(System.IO.Path.Combine(baseDir, "anything"));
        await Assert.That(rooted.filesystem.root.Absolute).IsEqualTo("/");
        // the "//x" OS-rooted form under a non-root maker is not the maker's folder
        await AssertSafe("/" + maker + "/sub");
    }

    // ── S6 a refused birth leaves nothing behind ───────────────────────────────

    [Test]
    public async Task S6_ARefusedBirth_CreatesNothing_AndLeavesTheMakerWorking()
    {
        var refusedAt = System.IO.Path.Combine(baseDir, "maker2");
        var absent = System.IO.Path.Combine(baseDir, "never-made", "sub");
        foreach (var path in new[] { refusedAt, absent, baseDir })
        {
            var (sub, refused) = Birth(path);
            await Assert.That(sub).IsNull();
            await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker");
        }
        await Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(refusedAt, ".data"))).IsFalse();
        await Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(baseDir, "never-made"))).IsFalse();
        await Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(baseDir, ".data"))).IsFalse();

        // the maker's actors were not touched by the failed births
        await Assert.That(app.actor.list.System.CancellationToken.IsCancellationRequested).IsFalse();
        await Assert.That(app.actor.list.User.CancellationToken.IsCancellationRequested).IsFalse();
        await Assert.That(await Passes(app.actor.list.User.Context, M("inside", "x"), Verb.write)).IsTrue();
    }

    // ── S7 the process parent rooted at "/" ────────────────────────────────────

    [Test]
    public async Task S7_ATestsAppMadeFromTheParentRootedAtSlash_IsGrantedNothingByIt()
    {
        var os = "/etc/hostname";
        // what the parent's own actors reach: the whole disk is in-root for them
        await Assert.That(await Passes(global::PLang.Tests.Parent.Actor.Context, os, Verb.read)).IsTrue();

        // the test's app, made from it: outside its own root a read asks, and 'n' denies; the parent's width is not inherited
        var user = Mute(app.actor.list.User);
        await Assert.That(await Passes(app.actor.list.User.Context, os, Verb.read)).IsFalse();
        await Assert.That(user.Questions.Count).IsGreaterThan(0);
        var system = Mute(app.actor.list.System);
        await Assert.That(await Passes(app.actor.list.System.Context, os, Verb.write)).IsFalse();

        // its stores folder is its own; its standing grants are the role's, on the os and on its own stores only
        await Assert.That(app.filesystem.store.Absolute).IsEqualTo(System.IO.Path.Combine(app.filesystem.root.Absolute, ".data"));
        var field = typeof(global::app.actor.permission.@this).GetField("_standing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var actor in new[] { app.actor.list.System, app.actor.list.User })
        {
            var paths = ((System.Collections.IEnumerable)field.GetValue(actor.Permission)!).Cast<global::app.data.@this>()
                .Select(d => ((Grant)d.Peek()!).Path).ToList();
            foreach (var path in paths)
                await Assert.That(path.StartsWith(app.filesystem.os.Absolute, System.StringComparison.Ordinal)
                                  || path.StartsWith(app.filesystem.store.Absolute, System.StringComparison.Ordinal)).IsTrue();
        }

        // a sibling app's stores (also under the root parent) are not reachable by this app's actors
        var sibling = System.IO.Path.Combine(baseDir, "maker2");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(sibling, ".data"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(sibling, ".data", "k"), "secret");
        await Assert.That(await Passes(app.actor.list.User.Context, System.IO.Path.Combine(sibling, ".data", "k"), Verb.read)).IsFalse();
        await Assert.That(await Passes(app.actor.list.System.Context, System.IO.Path.Combine(sibling, ".data", "k"), Verb.read)).IsFalse();
    }
}
