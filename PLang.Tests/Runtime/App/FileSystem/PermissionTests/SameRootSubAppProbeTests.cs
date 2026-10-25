using Verb = global::app.type.item.permission.Verb;
using Grant = global::app.type.item.permission.@this;
using MatchMode = global::app.type.item.permission.Match;
using ActorName = global::app.actor.Name;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Security probes (decision 48, fast-apps): (Q1) a sub app made at its maker's OWN root — its System stands the
/// stores' folder, which is the maker's .data — does the maker's User reach the maker's stores through it, beyond what
/// the maker's User already gets by naming the System in its own app (313f: accepted)? (Q2) the CopyTo/MoveTo ask path
/// as a view's first touch. (Q3) Saved() as a view's first touch. Each asserts the SAFE behaviour: red is a hole.
public class SameRootSubAppProbeTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string outside;
    private readonly global::app.@this app;
    private readonly Asking userChannel;

    public SameRootSubAppProbeTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sr-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sr-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "o.txt"), "outside");
        // NO Permission access here: every test below starts with a view that was never made
        app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning();
        userChannel = new Asking(app.actor.list.User.Context);
        app.actor.list.User.Channel.Register(userChannel);
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
    private global::app.actor.context.@this User => app.actor.list.User.Context;
    private global::app.actor.context.@this System_ => app.actor.list.System.Context;
    private string StoreFile => At(".data", "setting", "data.sqlite");

    private static global::app.data.@this<global::app.type.item.choice.@this<ActorName>> Named(global::app.actor.context.@this ctx, ActorName name)
        => new("", ctx.Choice(name), context: ctx);

    // a read/write/delete on the stores file as the given context: did the gate let it through, without an ask
    private static async Task<bool> Passes(global::app.actor.context.@this ctx, string path, Verb verb)
        => (await ctx.File(path).Authorize(verb, ctx)).Success;

    // ── Q1 a sub app at its maker's own root ──────────────────────────────────

    [Test]
    public async Task Q1a_ASubAppAtTheMakersRoot_ItsSystemStandsTheStores_ItsUserDoesNot()
    {
        await using var sub = new global::app.@this(root, app.actor.list.User);
        // the sub's own stores folder IS the maker's .data
        await Assert.That(sub.filesystem.store.Absolute).IsEqualTo(app.filesystem.store.Absolute);

        // its System: the stores (its role) — by design
        foreach (var verb in new[] { Verb.read, Verb.write, Verb.delete })
            await Assert.That(await Passes(sub.actor.list.System.Context, StoreFile, verb)).IsTrue();
        await Assert.That(await Passes(sub.actor.list.System.Context, At(".data"), Verb.read)).IsTrue();

        // its User: refused, as the maker's User is
        foreach (var verb in new[] { Verb.read, Verb.write, Verb.delete })
            await Assert.That(await Passes(sub.actor.list.User.Context, StoreFile, verb)).IsFalse();
        await Assert.That(await Passes(sub.actor.list.User.Context, At(".data"), Verb.read)).IsFalse();
    }

    [Test]
    public async Task Q1b_TheMakersOwnUser_IsStillRefusedOnTheStores_WhateverTheSubAppIs()
    {
        await using var sub = new global::app.@this(root, app.actor.list.User);
        _ = sub.actor.list.System.Permission;   // the sub's views are made and asked
        foreach (var verb in new[] { Verb.read, Verb.write, Verb.delete })
            await Assert.That(await Passes(User, StoreFile, verb)).IsFalse();
        await Assert.That(userChannel.Questions.Count).IsEqualTo(0);   // refused, never asked
        // the maker's System view is unchanged by the sub app (it stood the stores itself)
        await Assert.That(await Passes(System_, StoreFile, Verb.read)).IsTrue();
    }

    // (c) the maker's User calls INTO the sub app and names the System there; (d) the BASELINE: it names the System in its own
    // app (313f accepts it). The sub app is an escalation only if (c) reaches what (d) does not.
    [Test]
    public async Task Q1c_NamingTheSystemInTheSubApp_ReachesNoMoreThanNamingItInTheOwnApp()
    {
        await using var sub = new global::app.@this(root, app.actor.list.User);

        var viaOwn = new List<(Verb, bool)>();
        var viaSub = new List<(Verb, bool)>();
        foreach (var verb in new[] { Verb.read, Verb.write, Verb.delete })
        {
            // (d) the baseline: A's User names the System in A
            var own = await app.actor.list.Use(Named(User, ActorName.system), User,
                async runs => (await runs.File(StoreFile).Authorize(verb, runs)));
            viaOwn.Add((verb, own.Success));
            // (c) the sub app: its User (running the maker's call) names the System in the sub app
            var inSub = await sub.actor.list.Use(Named(sub.actor.list.User.Context, ActorName.system), sub.actor.list.User.Context,
                async runs => (await runs.File(StoreFile).Authorize(verb, runs)));
            viaSub.Add((verb, inSub.Success));
        }

        // the baseline is yes (313f). The sub app must add nothing: the same answers, verb by verb
        await Assert.That(viaOwn.All(r => r.Item2)).IsTrue();
        await Assert.That(viaSub.Select(r => r.Item2).ToArray()).IsEquivalentTo(viaOwn.Select(r => r.Item2).ToArray());
    }

    // decision 4: the sub app's System answers as its caller — does it read/sign with the maker's identity, not its own
    [Test]
    public async Task Q1d_TheSubAppsSystem_AnswersAsItsCallersIdentity_NotAnotherOne()
    {
        await using var sub = new global::app.@this(root, app.actor.list.User);
        var makers = app.actor.list.System.DefaultIdentity;
        var subs = sub.actor.list.System.DefaultIdentity;
        await Assert.That(makers).IsNotNull();
        await Assert.That(subs).IsNotNull();
        await Assert.That(subs!.PublicKey).IsEqualTo(makers!.PublicKey);
    }

    // ── Q2 the CopyTo / MoveTo ask path as a view's first touch ───────────────

    private string OsFile()
    {
        var os = app.filesystem.os.Absolute;
        return System.IO.Directory.EnumerateFiles(os, "*", System.IO.SearchOption.AllDirectories).First();
    }

    [Test]
    public async Task Q2a_AUsersFirstTouchIsACopyFromOutsideTheRoot_IsAsked()
    {
        var copied = await User.File(Out("o.txt")).CopyTo(User.File(At("in.txt")), overwrite: true, subfolder: false, User);
        await Assert.That(userChannel.Questions.Count).IsGreaterThan(0);   // outside the root: asked
        await Assert.That(copied.Success).IsFalse();                       // and 'n' stops it
        await Assert.That(System.IO.File.Exists(At("in.txt"))).IsFalse();
    }

    [Test]
    public async Task Q2b_AUsersFirstTouchIsACopyFromTheOs_IsGrantedWithoutAnAsk()
    {
        var copied = await User.File(OsFile()).CopyTo(User.File(At("fromos.txt")), overwrite: true, subfolder: false, User);
        await Assert.That(userChannel.Questions.Count).IsEqualTo(0);
        await Assert.That(copied.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(At("fromos.txt"))).IsTrue();
    }

    [Test]
    public async Task Q2c_AUsersFirstTouchIsACopyOrMoveIntoOrOutOfTheStores_IsRefusedWithNoAsk()
    {
        System.IO.Directory.CreateDirectory(At(".data"));
        System.IO.File.WriteAllText(At(".data", "a.txt"), "secret");
        System.IO.File.WriteAllText(At("mine.txt"), "mine");

        var into = await User.File(At("mine.txt")).CopyTo(User.File(At(".data", "b.txt")), overwrite: true, subfolder: false, User);
        var outOf = await User.File(At(".data", "a.txt")).CopyTo(User.File(At("stolen.txt")), overwrite: true, subfolder: false, User);
        var moveOut = await User.File(At(".data", "a.txt")).MoveTo(User.File(At("stolen2.txt")), overwrite: true, User);
        var moveIn = await User.File(At("mine.txt")).MoveTo(User.File(At(".data", "c.txt")), overwrite: true, User);

        await Assert.That(into.Success).IsFalse();
        await Assert.That(outOf.Success).IsFalse();
        await Assert.That(moveOut.Success).IsFalse();
        await Assert.That(moveIn.Success).IsFalse();
        await Assert.That(userChannel.Questions.Count).IsEqualTo(0);          // refused, never asked
        await Assert.That(System.IO.File.Exists(At("stolen.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(At("stolen2.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(At(".data", "b.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(At(".data", "c.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(At(".data", "a.txt"))).IsTrue();
    }

    [Test]
    public async Task Q2d_TheSystemsFirstTouchIsACopyOrMoveUnderTheStores_IsGrantedWithoutAnAsk()
    {
        System.IO.Directory.CreateDirectory(At(".data"));
        System.IO.File.WriteAllText(At(".data", "a.txt"), "secret");

        var copied = await System_.File(At(".data", "a.txt")).CopyTo(System_.File(At(".data", "b.txt")), overwrite: true, subfolder: false, System_);
        var moved = await System_.File(At(".data", "b.txt")).MoveTo(System_.File(At(".data", "c.txt")), overwrite: true, System_);

        await Assert.That(copied.Success).IsTrue();
        await Assert.That(moved.Success).IsTrue();
        await Assert.That(System.IO.File.Exists(At(".data", "c.txt"))).IsTrue();
        await Assert.That(System.IO.File.Exists(At(".data", "b.txt"))).IsFalse();
    }

    [Test]
    public async Task Q2e_TheSystemsFirstTouchIsACopyOrMoveUnderTheOs_IsGrantedWithoutAnAsk()
    {
        var osFile = OsFile();
        var dest = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(osFile)!, "plang-sr-probe-" + System.Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var copied = await System_.File(osFile).CopyTo(System_.File(dest), overwrite: true, subfolder: false, System_);
            await Assert.That(copied.Success).IsTrue();
            await Assert.That(System.IO.File.Exists(dest)).IsTrue();
            var moved = await System_.File(dest).MoveTo(System_.File(dest + ".moved"), overwrite: true, System_);
            await Assert.That(moved.Success).IsTrue();
        }
        finally
        {
            foreach (var f in new[] { dest, dest + ".moved" }) if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
        }
    }

    // ── Q3 Saved() as a view's first touch ────────────────────────────────────

    [Test]
    [Arguments("System")]
    [Arguments("User")]
    public async Task Q3a_AFirstTouchThatIsAPersistedAdd_StandsTheGrantsFirst_AndTheGrantIsFoundLater(string who)
    {
        var actor = who == "System" ? app.actor.list.System : app.actor.list.User;
        var ctx = actor.Context;
        var elsewhere = Out("kept");
        var grant = new Grant(actor.Name, elsewhere, Grant.AllVerbs, MatchMode.exact, app.type.list[typeof(Grant)]);

        // the very first thing this view is asked: add a grant that is saved to the actor's [Own] permission row
        await actor.Permission.Add(new global::app.data.@this<Grant>("", grant, context: ctx), persist: true);

        await Assert.That(await actor.Permission.Find(app.filesystem.os.Combine("x").Absolute, Verb.read)).IsNotNull();
        await Assert.That(await actor.Permission.Find(elsewhere, Verb.write)).IsNotNull();
        if (who == "User")
        {
            await Assert.That(await actor.Permission.Find(app.filesystem.os.Combine("x").Absolute, Verb.write)).IsNull();
            await Assert.That(await actor.Permission.Find(StoreFile, Verb.read)).IsNull();
        }
        else
            await Assert.That(await actor.Permission.Find(StoreFile, Verb.read)).IsNotNull();
    }

    [Test]
    [Arguments("System")]
    [Arguments("User")]
    public async Task Q3b_AFirstTouchThatIsFindOnAnUnknownPath_ReadsTheSavedRowWithoutBreakingTheStood(string who)
    {
        var actor = who == "System" ? app.actor.list.System : app.actor.list.User;
        // Find on a path nothing stands reaches Saved() (the [Own] row) as the view's first touch
        await Assert.That(await actor.Permission.Find(Out("unknown"), Verb.read)).IsNull();
        await Assert.That(await actor.Permission.Find(app.filesystem.os.Combine("x").Absolute, Verb.read)).IsNotNull();
        await Assert.That(await actor.Permission.Find(StoreFile, Verb.read) != null).IsEqualTo(who == "System");
    }

    [Test]
    [Arguments("System")]
    [Arguments("User")]
    public async Task Q3c_AFirstTouchThatIsARevoke_ReadsTheSavedRowAndLeavesTheStoodAlone(string who)
    {
        var actor = who == "System" ? app.actor.list.System : app.actor.list.User;
        var gone = new Grant(actor.Name, Out("never-held"), Grant.AllVerbs, MatchMode.exact, app.type.list[typeof(Grant)]);
        await Assert.That(await actor.Permission.Revoke(gone)).IsFalse();
        await Assert.That(await actor.Permission.Find(app.filesystem.os.Combine("x").Absolute, Verb.read)).IsNotNull();
        await Assert.That(await actor.Permission.Find(StoreFile, Verb.write) != null).IsEqualTo(who == "System");
    }
}
