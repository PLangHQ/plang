using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Security probes (decision 51, fast-apps, S4): a sub app is never made in its maker's stores folder or under it, on
/// the LANDED roots. These are the shapes around the stores folder: a link that lands in it, a case-differing name, a
/// lookalike name beside it, `..` reaching it, a chain of makers, and the maker's own root. Each asserts the SAFE
/// behaviour: red is a hole.
public class SubAppStoresShapesProbeTests : System.IAsyncDisposable
{
    private readonly string baseDir;
    private readonly string maker;
    private readonly global::app.@this app;
    private readonly List<global::app.@this> born = new();

    public SubAppStoresShapesProbeTests()
    {
        baseDir = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ss-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        maker = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "maker")).FullName;
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker, ".data", "setting"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(maker, ".data", "identity.key"), "secret");
        System.IO.File.WriteAllText(System.IO.Path.Combine(maker, ".data", "setting", "data.sqlite"), "rows");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker, "sub"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker, "nest"));
        app = new global::app.@this(maker, global::PLang.Tests.Parent.Actor).TestSigning();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        foreach (var b in born) await b.DisposeAsync();
        await app.DisposeAsync();
        try { System.IO.Directory.Delete(baseDir, true); } catch { /* links to folders are removed alone */ }
    }

    private string M(params string[] parts) => System.IO.Path.Combine([maker, .. parts]);

    private void Link(string at, string target) => System.IO.Directory.CreateSymbolicLink(at, target);

    // a sub app made by `maker`'s actor at `path`: born, or refused with its error
    private (global::app.@this? sub, global::app.error.AppException? refused) BirthBy(global::app.actor.@this actor, string path)
    {
        try
        {
            var sub = new global::app.@this(path, actor);
            born.Add(sub);
            return (sub, null);
        }
        catch (global::app.error.AppException ex) { return (null, ex); }
    }

    private (global::app.@this? sub, global::app.error.AppException? refused) Birth(string path) => BirthBy(app.actor.list.User, path);

    private async Task AssertRefused(string path, global::app.actor.@this? by = null)
    {
        var (sub, refused) = BirthBy(by ?? app.actor.list.User, path);
        await Assert.That(sub).IsNull();
        await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker");
    }

    private async Task AssertTaken(string path, global::app.actor.@this? by = null)
    {
        var (sub, refused) = BirthBy(by ?? app.actor.list.User, path);
        await Assert.That(refused).IsNull();
        await Assert.That(sub).IsNotNull();
    }

    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public Asking(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
            => Task.FromResult(action.Context.Ok("n"));
    }

    private static void Mute(global::app.actor.@this actor) => actor.Channel.Register(new Asking(actor.Context));

    private static async Task<bool> Passes(global::app.actor.context.@this ctx, string path, Verb verb)
        => (await ctx.File(path).Authorize(verb, ctx)).Success;

    // the maker's stores, as a sub app's two actors try to reach them: nothing passes
    private async Task AssertNoReachIntoTheMakersStores(global::app.@this sub)
    {
        foreach (var actor in new[] { sub.actor.list.User, sub.actor.list.System })
        {
            Mute(actor);
            foreach (var verb in new[] { Verb.read, Verb.write, Verb.delete })
            {
                await Assert.That(await Passes(actor.Context, M(".data", "identity.key"), verb)).IsFalse();
                await Assert.That(await Passes(actor.Context, M(".data"), verb)).IsFalse();
            }
        }
    }

    // ── a) a link inside the maker that lands in the stores ────────────────────

    [Test]
    public async Task A_ALinkInTheMakerThatLandsInTheStores_IsRefused()
    {
        Link(M("lnk"), M(".data"));
        await AssertRefused(M("lnk"));
        await AssertRefused(M("lnk", "new"));              // under the link: the deepest existing part lands in the stores
        await AssertRefused(M("lnk", "setting"));
    }

    [Test]
    public async Task A_ALinkToAFolderUnderTheStores_IsRefused()
    {
        Link(M("lnk2"), M(".data", "setting"));
        await AssertRefused(M("lnk2"));
    }

    [Test]
    public async Task A_ALinkInANestedFolderThatLandsInTheStores_IsRefused()
    {
        Link(M("nest", "deep"), M(".data"));
        await AssertRefused(M("nest", "deep"));
        await AssertRefused(M("nest", "deep", "x"));
    }

    [Test]
    public async Task A_ATwoHopChainLandingInTheStores_IsRefused()
    {
        Link(M("h1"), M(".data"));
        Link(M("h2"), M("h1"));
        await AssertRefused(M("h2"));
    }

    [Test]
    public async Task A_ARelativeLinkLandingInTheStores_IsRefused()
    {
        Link(M("nest", "rel"), "../.data");
        await AssertRefused(M("nest", "rel"));
    }

    // ── b) a case-differing name ───────────────────────────────────────────────

    // On a case-sensitive disk `.Data` is another folder (taken, and it must not reach the real stores). On a
    // case-insensitive disk it IS the stores, and must be refused. Either way: nothing born reaches the real stores.
    [Test]
    [Arguments(".Data")]
    [Arguments(".DATA")]
    public async Task B_ACaseDifferingStoresName_IsEitherRefusedOrAnotherFolderThatReachesNothing(string name)
    {
        var caseInsensitive = System.IO.Directory.Exists(M(".DATA"));
        var (sub, refused) = Birth(M(name));
        if (caseInsensitive)
        {
            await Assert.That(sub).IsNull();
            await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker");
            return;
        }
        await Assert.That(refused).IsNull();
        await AssertNoReachIntoTheMakersStores(sub!);
    }

    // ── c) lookalike names beside the stores ───────────────────────────────────

    [Test]
    [Arguments(".data2")]
    [Arguments("data")]
    [Arguments(".data.bak")]
    [Arguments(".dat")]
    [Arguments(".data ")]
    public async Task C_ALookalikeBesideTheStores_IsTaken_AndItsStoresAreItsOwn_AndItReachesNothingOfTheMakers(string name)
    {
        var (sub, refused) = Birth(M(name));
        await Assert.That(refused).IsNull();
        await Assert.That(sub!.filesystem.store.Absolute).IsNotEqualTo(M(".data"));
        await Assert.That(sub.filesystem.store.IsUnder(app.filesystem.store).Value).IsFalse();
        await AssertNoReachIntoTheMakersStores(sub);
    }

    // ── d) the stores reached through `..` ─────────────────────────────────────

    [Test]
    public async Task D_TheStoresReachedThroughDotDotFromAFolderUnderTheMaker_AreRefused()
    {
        await AssertRefused(maker + "/sub/../.data");
        await AssertRefused(maker + "/sub/../.data/setting");
        await AssertRefused(maker + "/sub/./../.data/x/..");
        await AssertRefused(maker + "\\sub\\..\\.data");
    }

    [Test]
    public async Task D_ADotDotOutOfTheStoresIntoAFolderUnderTheMaker_IsTaken()
    {
        await AssertTaken(maker + "/.data/../sub");
    }

    // ── e) a sub app of a sub app: the chain of makers ─────────────────────────

    [Test]
    public async Task E_ASubAppOfASubApp_IsHeldToItsImmediateMakersStores_AndToItsMakersFolder()
    {
        var (b, refusedB) = Birth(M("sub"));
        await Assert.That(refusedB).IsNull();
        var byB = b!.actor.list.User;

        await AssertTaken(M("sub", "c"), byB);            // under B
        await AssertTaken(M("sub"), byB);                 // B's own root
        await AssertRefused(M("sub", ".data"), byB);      // B's stores
        await AssertRefused(M("sub", ".data", "x"), byB);
        await AssertRefused(M(".data"), byB);             // A's stores: outside B's folder
        await AssertRefused(maker, byB);                  // above B
        await AssertRefused(M("nest"), byB);              // beside B
    }

    [Test]
    public async Task E_ASubAppOfASubApp_AtASharedRoot_IsRefusedInTheSharedStores()
    {
        // B at A's own root: B's stores ARE A's. C by B at A/.data is refused as B's stores; at A/x taken.
        var (b, refusedB) = Birth(maker);
        await Assert.That(refusedB).IsNull();
        var byB = b!.actor.list.User;
        await AssertRefused(M(".data"), byB);
        await AssertRefused(M(".data", "setting"), byB);
        await AssertTaken(M("sub"), byB);
        await AssertTaken(maker, byB);
    }

    [Test]
    public async Task E_AThreeLevelChain_StillRefusesEveryMakersStores()
    {
        var (b, _) = Birth(M("sub"));
        var (c, refusedC) = BirthBy(b!.actor.list.User, M("sub", "c"));
        await Assert.That(refusedC).IsNull();
        var byC = c!.actor.list.User;
        await AssertTaken(M("sub", "c", "d"), byC);
        await AssertRefused(M("sub", "c", ".data"), byC);  // C's stores
        await AssertRefused(M("sub", ".data"), byC);       // B's stores: outside C
        await AssertRefused(M(".data"), byC);              // A's stores: outside C
    }

    // ── f) the maker's root itself ─────────────────────────────────────────────

    [Test]
    public async Task F_TheMakersOwnRoot_IsStillTaken_AndItsStoresAreTheMakers()
    {
        var (sub, refused) = Birth(maker);
        await Assert.That(refused).IsNull();
        await Assert.That(sub!.filesystem.store.Absolute).IsEqualTo(app.filesystem.store.Absolute);
        await Assert.That(sub.filesystem.root.Absolute).IsEqualTo(app.filesystem.root.Absolute);
        // trailing separator and a `.` form of the same root
        await AssertTaken(maker + "/");
        await AssertTaken(maker + "/.");
    }
}
