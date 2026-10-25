using IdentityRecord = global::app.module.identity.Identity;

namespace PLang.Tests.Runtime.App.IdentityLeak;

/// Security probes (fast-apps full pass, decision 92): a sub app answers as the actor that made it (decision 4), its
/// runtime bindings are held on its own actors (26), what it shares with its maker is refused to change (36–41), it
/// tests from its birth when its maker tests (84). Each test asserts the SAFE behaviour: red is a hole (or, where the
/// name says LATENT, a hole no door reaches yet).
public class SubAppFullPassProbeTests : System.IAsyncDisposable
{
    private const string Key = "test-private-key";   // TestSigning's key mock hands every pair this private key

    private readonly string dir;
    private readonly List<global::app.@this> born = new();

    public SubAppFullPassProbeTests()
    {
        dir = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-fp-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        foreach (var b in born) await b.DisposeAsync();
        try { System.IO.Directory.Delete(dir, true); } catch { /* best effort */ }
    }

    // an app made from the code, in a folder of its own, on disk (not testing)
    private global::app.@this Maker(string name = "maker")
    {
        var path = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, name)).FullName;
        var app = new global::app.@this(path).TestSigning();
        born.Add(app);
        return app;
    }

    private global::app.@this SubOf(global::app.actor.@this actor, string? path = null, bool open = true)
    {
        var sub = new global::app.@this(path ?? actor.App.filesystem.root.Absolute, actor).TestSigning();
        if (open) sub.test.list.Open(sub.actor.list.User);
        born.Add(sub);
        return sub;
    }

    private static async Task<global::app.data.@this> Run(global::app.actor.context.@this ctx, string formal)
        => await (await ctx.Action(formal)).Start(ctx);

    // ---- decision 4: a sub app's System answers as its maker's actor ----

    // the baseline the sub must not exceed: the maker's User already signs as the app's default identity
    [Test]
    public async Task D4_TheSubsSystem_IsTheMakersUsersIdentity_NoMoreThanTheUserAlreadyHas()
    {
        var maker = Maker();
        var makersDefault = maker.actor.list.System.DefaultIdentity!;
        var sub = SubOf(maker.actor.list.User);

        var subs = sub.actor.list.System.DefaultIdentity;

        await Assert.That(subs).IsNotNull();
        await Assert.That(subs!.PublicKey).IsEqualTo(makersDefault.PublicKey);
        // what the maker's user holds of it as a step: the public key only
        var userSees = (IdentityRecord)(await (await maker.actor.list.User.Context.Variable.Get("MyIdentity")).Value())!;
        await Assert.That(userSees.PrivateKey).IsNotEqualTo(Key);
    }

    // a sub app's identity is not written into its own store (it is the maker's, not a row of the sub's)
    [Test]
    public async Task D4_TheMakersKey_IsNotWrittenIntoTheSubsStore()
    {
        var maker = Maker();
        _ = maker.actor.list.System.DefaultIdentity;
        var sub = SubOf(maker.actor.list.User);
        _ = sub.actor.list.System.DefaultIdentity;

        var setting = await sub.actor.list.System.Context.Setting.Get<global::app.module.identity.setting.@this>();
        await Assert.That(setting.Identity?.Count ?? 0).IsEqualTo(0);
    }

    // %Identity% / %MyIdentity% in a sub's User: public only
    [Test]
    public async Task D4_TheSubsUser_ReadsNoPrivateKey_ThroughItsVariables()
    {
        var maker = Maker();
        _ = maker.actor.list.System.DefaultIdentity;
        var sub = SubOf(maker.actor.list.User);

        var mine = (IdentityRecord)(await (await sub.actor.list.User.Context.Variable.Get("MyIdentity")).Value())!;
        await Assert.That(mine.PrivateKey).IsNotEqualTo(Key);
        var id = await sub.actor.list.User.Context.Variable.GetValue("Identity");
        await Assert.That(id?.ToString()).DoesNotContain(Key);
    }

    // identity.export in the sub: the sub's actor answers the question, and the key it hands is the MAKER's. The maker's
    // user never said yes. Safe = the maker's key is not what a sub's yes hands out.
    [Test]
    public async Task D4_ExportInTheSub_WithTheSubsYes_DoesNotHandTheMakersKey()
    {
        var maker = Maker();
        maker.actor.list.User.Identity = new IdentityRecord(maker.type.list[typeof(IdentityRecord)]) { Name = "makers", PublicKey = "MAKER-PUB", PrivateKey = "MAKER-PRIV" };
        var sub = SubOf(maker.actor.list.User).Consenting();

        var got = await Run(sub.actor.list.User.Context, "identity.export()");

        var key = got.Success ? ((IdentityRecord?)await got.Value())?.PrivateKey : null;
        await Assert.That(key).IsNotEqualTo("MAKER-PRIV").Because("the sub's yes handed the maker's private key");
    }

    // a change to identities in the sub stays in the sub's (in-memory) store
    [Test]
    public async Task D4_CreateInTheSub_NeverReachesTheMakersIdentities()
    {
        var maker = Maker();
        _ = maker.actor.list.System.DefaultIdentity;
        var sub = SubOf(maker.actor.list.User).Consenting();

        var made = await Run(sub.actor.list.User.Context, "identity.create(Name=\"intruder\")");
        await Assert.That(made.Success).IsTrue();

        var makers = await maker.actor.list.System.Context.Setting.Get<global::app.module.identity.setting.@this>();
        await Assert.That(makers.Identity!.Items(maker.actor.list.System.Context).Select(r => ((IdentityRecord)r.Peek()).Name)).DoesNotContain("intruder");
        var file = System.IO.Path.Combine(maker.filesystem.root.Absolute, ".data", "data.sqlite");
        await Assert.That(System.IO.File.Exists(file) && System.IO.File.ReadAllBytes(file).AsSpan().IndexOf("intruder"u8) >= 0).IsFalse();
    }

    // a maker's user with an identity of its own: the sub's System signs as it (by design); another actor's sub does not
    [Test]
    public async Task D4_AUsersOwnIdentity_IsTheSubsOnlyWhenThatUserMadeIt()
    {
        var maker = Maker();
        var theirs = new IdentityRecord(maker.type.list[typeof(IdentityRecord)]) { Name = "visitor", PublicKey = "VISITOR-PUB", PrivateKey = "VISITOR-PRIV" };
        maker.actor.list.User.Identity = theirs;

        var byUser = SubOf(maker.actor.list.User, System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker.filesystem.root.Absolute, "u")).FullName);
        var bySystem = SubOf(maker.actor.list.System, System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker.filesystem.root.Absolute, "s")).FullName);

        await Assert.That(byUser.actor.list.System.DefaultIdentity!.PublicKey).IsEqualTo("VISITOR-PUB");
        await Assert.That(bySystem.actor.list.System.DefaultIdentity!.PublicKey).IsNotEqualTo("VISITOR-PUB");
        // a sub's own set never changes its maker's
        byUser.actor.list.User.Identity = null;
        await Assert.That(maker.actor.list.User.Identity).IsSameReferenceAs(theirs);
    }

    // a chain: the grandchild answers as the child's User, which answers as the maker's
    [Test]
    public async Task D4_AChainOfMakers_AnswersAsTheNearestMakersActor()
    {
        var maker = Maker();
        var theirs = new IdentityRecord(maker.type.list[typeof(IdentityRecord)]) { Name = "visitor", PublicKey = "VISITOR-PUB", PrivateKey = "VISITOR-PRIV" };
        maker.actor.list.User.Identity = theirs;
        var sub = SubOf(maker.actor.list.User);
        var grand = SubOf(sub.actor.list.User, System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker.filesystem.root.Absolute, "g")).FullName);

        await Assert.That(grand.actor.list.System.DefaultIdentity!.PublicKey).IsEqualTo("VISITOR-PUB");
    }

    // ---- the hold (26) ----

    private static async Task<global::app.@event.binding.@this> Bind(global::app.actor.context.@this ctx, string @event,
        global::app.goal.step.action.@this call, string scope)
    {
        var bound = await Make.Action(ctx, "on", "event", ("Event", @event), ("When", "before"), ("Action", call), ("Scope", scope)).Start(ctx);
        await bound.IsSuccess();
        return (global::app.@event.binding.@this)bound.Peek();
    }

    private static async Task<global::app.data.@this> Work(global::app.@this app)
    {
        var ctx = app.actor.list.User.Context;
        var goal = Make.Goal(ctx, "Work", Make.Step("set %done% = true",
            Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "done", "variable"), ("Value", true))));
        return await goal.Start(ctx);
    }

    // the maker binds app-wide on the step type every app over its code shares: a sub's step never fires it
    [Test]
    public async Task H_AMakersAppWideBinding_NeverFiresInItsSub()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var ctx = maker.actor.list.User.Context;
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "OnlyInTheMaker"), "app");

        await (await Work(sub)).IsSuccess();
    }

    // a sub's binding never fires in its grandchild's sibling, nor in the maker, and the maker's hold is untouched
    [Test]
    public async Task H_ASubsBinding_LeavesTheMakerAndAChainNothing()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var cousin = SubOf(maker.actor.list.User, System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker.filesystem.root.Absolute, "c")).FullName);
        var ctx = sub.actor.list.User.Context;
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "OnlyInTheSub"), "app");

        await (await Work(cousin)).IsSuccess();
        await (await Work(maker)).IsSuccess();
        await Assert.That(maker.type.list["step"].on.start.before.Count).IsEqualTo(0);
        foreach (var a in maker.actor.list.Items())
            await Assert.That(a.on.Of(maker.type.list["step"])?.start.before.Count ?? 0).IsEqualTo(0);
    }

    // an actor-scoped binding of the sub's System never fires for the maker's or the sub's User
    [Test]
    public async Task H_AnActorScopedBinding_OfTheSubsSystem_NeverFiresForTheMakersSystem()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var ctx = sub.actor.list.System.Context;
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "OnlyInTheSub"), "actor");

        await (await Work(maker)).IsSuccess();
    }

    // ---- shared types, actions (36–41) ----

    // a sub app that adds an action to a module its maker holds is refused; the maker's module is unchanged
    [Test]
    public async Task C_ASubCannotAddAnActionToTheMakersModule()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var before = maker.module.Named("variable")?.Count ?? -1;

        var refused = Assert.Throws<global::app.error.AppException>(() => sub.module.Register("variable", "evilaction", typeof(object)));

        await Assert.That(refused!.Error.Key).IsEqualTo("ModuleShared");
        await Assert.That(maker.module.Named("variable")?.Count ?? -1).IsEqualTo(before);
        await Assert.That(maker.module.Named("variable")?["evilaction"]).IsNull();
    }

    // a sub app that takes out a module its maker holds is refused; the maker still has it
    [Test]
    public async Task C_ASubCannotRemoveTheMakersModule()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var module = sub.module.Named("variable") ?? maker.module.Named("variable");
        await Assert.That(module).IsNotNull();

        var refused = Assert.Throws<global::app.error.AppException>(() => module!.Remove(sub.actor.list.System.Context).AsTask().GetAwaiter().GetResult());

        await Assert.That(refused!.Error.Key).IsEqualTo("ModuleShared");
        await Assert.That(maker.module.Named("variable")).IsNotNull();
    }

    // the same refusal as a result when a sub app adds a kind to a type its maker holds
    [Test]
    public async Task C_ASubCannotAddAKindToTheMakersType()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var text = maker.type.list["text"];
        var kinds = text.kind.Keys.Count();
        var kind = new global::app.type.kind.@this(new global::app.Attributes.FormatAttribute("evil"), text);

        var refusal = ((global::app.origin.@this)maker.actor.list.User).Hold(kind);

        await Assert.That(refusal?.Key).IsEqualTo("TypesShared");
        await Assert.That(text.kind.Keys.Count()).IsEqualTo(kinds);
    }

    // a type a sub adds is its own: the maker and a sibling do not see it (a name the maker holds is not shadowed there)
    [Test]
    public async Task C_ATypeASubAdds_IsNotTheMakersNorASiblings()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        var sibling = SubOf(maker.actor.list.User, System.IO.Directory.CreateDirectory(System.IO.Path.Combine(maker.filesystem.root.Absolute, "sib")).FullName);
        await sub.type.list.Add(typeof(SharedGoalProbeType), sub.actor.list.System.Context).IsSuccess();

        await Assert.That(sub.type.list["sharedgoalprobetype"]).IsNotNull();
        await Assert.That(maker.type.list.Contains("sharedgoalprobetype")).IsFalse();
        await Assert.That(sibling.type.list.Contains("sharedgoalprobetype")).IsFalse();
    }

    [global::app.Attributes.PlangType("sharedgoalprobetype")]
    public sealed class SharedGoalProbeType : global::app.type.item.@this
    {
        public SharedGoalProbeType(global::app.type.@this type) : base(type) { }
    }

    // ---- origin.Testing (84): where a sub app's store lives ----

    private static string Sqlite(global::app.@this app) => System.IO.Path.Combine(app.filesystem.root.Absolute, ".data", "data.sqlite");

    private static async Task Touch(global::app.@this app)
    {
        var ctx = app.actor.list.System.Context;
        (await app.store.Set("probe", "k", new global::app.data.@this("k", "v", context: ctx))).Success.Equals(true);
    }

    // the runner's own order (born, session opened, its test setting read, run, ended): the maker's disk is never touched
    [Test]
    public async Task T_TheRunnersOrder_LeavesTheMakersDiskAlone()
    {
        var maker = Maker();
        var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        await sub.test.list.Open(null);
        await Touch(sub);
        await sub.DisposeAsync();

        await Assert.That(System.IO.File.Exists(Sqlite(maker))).IsFalse();
    }

    // the control: a running (non-testing) app's first touch makes its database file
    [Test]
    public async Task T_Control_ARunningAppsTouch_MakesItsFile()
    {
        var maker = Maker();
        await Touch(maker);
        await Assert.That(System.IO.File.Exists(Sqlite(maker))).IsTrue();
    }

    // a sub app of a testing app writes nothing to the disk of its (same) folder, down a chain
    [Test]
    public async Task T_ASubOfATestingApp_WritesNothingToDisk_DownAChain()
    {
        var maker = Maker();
        maker.test.list.Open(maker.actor.list.User);
        var sub = SubOf(maker.actor.list.User, open: false);
        var grand = SubOf(sub.actor.list.User, open: false);

        await Assert.That(sub.Mode.Value).IsEqualTo(global::app.Mode.Test);
        await Assert.That(grand.Mode.Value).IsEqualTo(global::app.Mode.Test);
        await Touch(sub);
        await Touch(grand);

        await Assert.That(System.IO.File.Exists(Sqlite(sub))).IsFalse();
    }

    // a sub app of a testing app stays testing when its own session is closed
    [Test]
    public async Task T_ASubOfATestingApp_KeepsTestingAfterItsSessionCloses()
    {
        var maker = Maker();
        maker.test.list.Open(maker.actor.list.User);
        var sub = SubOf(maker.actor.list.User);
        await sub.test.list.Close();

        await Assert.That(sub.Mode.Value).IsEqualTo(global::app.Mode.Test);
        await Touch(sub);
        await Assert.That(System.IO.File.Exists(Sqlite(sub))).IsFalse();
    }

    // LATENT: a sub app of a non-testing app tests only while its own session is open. The store is opened at its first
    // touch: after the session closes, that touch lands on the maker's .data on disk. Only the test runner makes sub apps
    // today and it opens the session at birth and never closes it before the app ends.
    [Test]
    [Skip("Backlog 10, where a sub app's stores live (317a): a sub app of a non-testing maker opens its maker's .data. The stores question's ready test: unskip it first.")]
    public async Task T_LATENT_ASubOfANonTestingApp_NeverWritesToTheMakersDisk_EvenAfterItsSessionCloses()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User);
        await sub.test.list.Close();

        await Touch(sub);

        await Assert.That(System.IO.File.Exists(Sqlite(maker))).IsFalse().Because("the sub's first store touch opened the maker's database file");
    }

    // a sub app of a non-testing app that is never opened for testing is a running app: it shares the maker's folder and
    // therefore its database. LATENT (the ctor is internal; the runner opens the session at birth)
    [Test]
    [Skip("Backlog 10, where a sub app's stores live (317a): a sub app of a non-testing maker opens its maker's .data. The stores question's ready test: unskip it first.")]
    public async Task T_LATENT_ASubOfANonTestingApp_NotYetOpened_DoesNotWriteTheMakersDisk()
    {
        var maker = Maker();
        var sub = SubOf(maker.actor.list.User, open: false);

        await Touch(sub);

        await Assert.That(System.IO.File.Exists(Sqlite(maker))).IsFalse();
    }
}
