using Verb = global::app.type.item.permission.Verb;
using Grant = global::app.type.item.permission.@this;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Security probes for c1837a736 (an actor's role stands its grants at its permission view's first ask): the grants
/// that exist, for whom, on which folder; that no ask can come before they are stood; a sub app's view standing only on
/// its own folders. Each asserts the SAFE behaviour: red is a hole.
public class RoleStandingProbeTests
{
    private static string Sep => System.IO.Path.DirectorySeparatorChar.ToString();

    // the view's standing in-memory grants, as (path, match, verbs) — the private list a view keeps them in
    private static List<(string Path, string Match, string Verbs)> Held(global::app.actor.@this actor)
    {
        var field = typeof(global::app.actor.permission.@this).GetField("_standing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var list = (System.Collections.IEnumerable)field.GetValue(actor.Permission)!;
        var held = new List<(string, string, string)>();
        foreach (var d in list)
            if (((global::app.data.@this)d).Peek() is Grant g)
                held.Add((g.Path, g.Match.ToString(), string.Join("+", g.Verbs.Select(v => v.ToString()).OrderBy(v => v))));
        return held.OrderBy(h => h.Item1).ThenBy(h => h.Item2).ToList();
    }

    private static async Task<string> Stands(global::app.actor.@this actor, string folder)
    {
        var allowed = new List<string>();
        foreach (var at in new[] { folder, folder + Sep + "x" })
            foreach (var verb in System.Enum.GetValues<Verb>())
                if (await actor.Permission.Find(at, verb) != null) allowed.Add((at == folder ? "" : "under ") + verb);
        return string.Join(", ", allowed);
    }

    // ── P1 grants unchanged ────────────────────────────────────────────────────

    [Test]
    public async Task P1_TheSystem_StandsExactlyTheOsAndStoresGrants_AndNoOther()
    {
        await using var app = new global::app.@this("/p1root", global::PLang.Tests.Parent.Actor).Testing();
        var os = app.filesystem.os.Absolute;
        var store = app.filesystem.store.Absolute;

        // four grants: the folder itself (exact) and everything under it (glob), for the os and for the stores
        var held = Held(app.actor.list.System);
        await Assert.That(held.Count).IsEqualTo(4);
        await Assert.That(held.Select(h => h.Path).OrderBy(p => p).ToArray())
            .IsEquivalentTo(new[] { os, os + Sep + "**", store, store + Sep + "**" }.OrderBy(p => p).ToArray());
        await Assert.That(held.Where(h => h.Path.StartsWith(os)).All(h => h.Verbs == "delete+execute+read+write")).IsTrue();
        await Assert.That(held.Where(h => h.Path.StartsWith(store)).All(h => h.Verbs == "delete+read+write")).IsTrue();

        // nothing else is stood: outside the two folders no verb is answered
        foreach (var outside in new[] { "/etc", "/etc/passwd", "/tmp/x", "/p1root", "/p1root/x", "/p1root/y/.data" })
            await Assert.That(await Stands(app.actor.list.System, outside)).IsEqualTo("");
    }

    [Test]
    public async Task P1_AUser_StandsOnlyOsReadAndExecute_NothingOnTheStores()
    {
        await using var app = new global::app.@this("/p1root", global::PLang.Tests.Parent.Actor).Testing();
        var os = app.filesystem.os.Absolute;
        var store = app.filesystem.store.Absolute;

        var held = Held(app.actor.list.User);
        await Assert.That(held.Count).IsEqualTo(2);
        await Assert.That(held.Select(h => h.Path).OrderBy(p => p).ToArray())
            .IsEquivalentTo(new[] { os, os + Sep + "**" }.OrderBy(p => p).ToArray());
        await Assert.That(held.All(h => h.Verbs == "execute+read")).IsTrue();

        await Assert.That(await Stands(app.actor.list.User, os)).IsEqualTo("read, execute, under read, under execute");
        await Assert.That(await Stands(app.actor.list.User, store)).IsEqualTo("");
        foreach (var outside in new[] { "/etc", "/p1root/x" })
            await Assert.That(await Stands(app.actor.list.User, outside)).IsEqualTo("");
    }

    // ── P2 never asked before stood ───────────────────────────────────────────

    [Test]
    [Arguments("System", "Authorize")]
    [Arguments("User", "Authorize")]
    [Arguments("System", "Find")]
    [Arguments("User", "Find")]
    [Arguments("System", "Add")]
    [Arguments("User", "Add")]
    [Arguments("System", "Revoke")]
    [Arguments("User", "Revoke")]
    public async Task P2_TheFirstTouchOfAView_NeverMissesTheOsGrant(string who, string first)
    {
        await using var app = new global::app.@this("/p2root", global::PLang.Tests.Parent.Actor).Testing();
        var actor = who == "System" ? app.actor.list.System : app.actor.list.User;
        var ctx = actor.Context;
        var osFile = app.filesystem.os.Combine("probe.txt");

        // the very first thing this view is asked is the named operation
        switch (first)
        {
            case "Add":
                await actor.Permission.Add(new global::app.data.@this<Grant>("",
                    new Grant(actor.Name, "/p2root-elsewhere", Grant.AllVerbs, MatchMode.exact, app.type.list[typeof(Grant)]), context: ctx), persist: false);
                break;
            case "Revoke":
                await actor.Permission.Revoke(new Grant(actor.Name, "/p2root-elsewhere", Grant.AllVerbs, MatchMode.exact, app.type.list[typeof(Grant)]));
                break;
        }

        // then the os read: granted by the standing grant, never raised as an ask (the test's input is closed, so an ask is a denial)
        await Assert.That(await actor.Permission.Find(osFile.Absolute, Verb.read)).IsNotNull();
        var authorized = await osFile.Authorize(Verb.read, ctx);
        await Assert.That(authorized.Success).IsTrue();
        if (who == "System")
        {
            var write = await osFile.Authorize(Verb.write, ctx);
            await Assert.That(write.Success).IsTrue();
        }
    }

    [Test]
    [Arguments("System")]
    [Arguments("User")]
    public async Task P2_SixtyFourThreadsMakingTheFirstAskOfOneView_SeeOneViewWithItsGrants(string who)
    {
        await using var app = new global::app.@this("/p2race", global::PLang.Tests.Parent.Actor).Testing();
        var actor = who == "System" ? app.actor.list.System : app.actor.list.User;
        var osFile = app.filesystem.os.Combine("probe.txt");
        using var gate = new System.Threading.ManualResetEventSlim(false);

        var work = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
        {
            gate.Wait();
            var view = actor.Permission;
            var found = await view.Find(osFile.Absolute, Verb.read);
            var ok = found != null;
            if (who == "System") ok &= await view.Find(osFile.Absolute, Verb.write) != null;
            return (view, ok);
        })).ToArray();
        gate.Set();
        var results = await Task.WhenAll(work);

        await Assert.That(results.Select(r => r.view).Distinct().Count()).IsEqualTo(1);
        await Assert.That(results.All(r => r.ok)).IsTrue();
        await Assert.That(Held(actor).Count).IsEqualTo(who == "System" ? 4 : 2);
    }

    // ── P3 re-entrancy / context ──────────────────────────────────────────────

    [Test]
    public async Task P3_TheViewIsFirstAskedStraightAfterTheActorIsBorn_AndItsGrantsHoldTheActorsOwnContext()
    {
        var app = global::PLang.Tests.Parent.Actor.App;
        foreach (var role in new global::app.actor.role.@this[] { new global::app.actor.role.system(), new global::app.actor.role.user() })
        {
            var actor = new global::app.actor.@this(role, app);
            // the first thing asked of a bare actor is its permission view: must not throw nor recurse
            var view = actor.Permission;
            await Assert.That(view).IsNotNull();
            var field = typeof(global::app.actor.permission.@this).GetField("_standing",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var contexts = ((System.Collections.IEnumerable)field.GetValue(view)!).Cast<global::app.data.@this>()
                .Select(d => d.Context).ToList();
            await Assert.That(contexts.Count).IsGreaterThan(0);
            await Assert.That(contexts.All(c => ReferenceEquals(c, actor.Context))).IsTrue();
        }
    }

    [Test]
    public async Task P3_AnAppBornAndItsActorsContextsMade_NeverFailsOnTheFirstAsk()
    {
        // the whole birth: actor list, code providers, modules, goals, tests are made; then both views are asked
        var app = new global::app.@this("/p3root", global::PLang.Tests.Parent.Actor);
        await using (app)
        {
            var system = app.actor.list.System;
            var user = app.actor.list.User;
            await Assert.That(system.Context.Actor).IsEqualTo(system);
            await Assert.That(user.Context.Actor).IsEqualTo(user);
            await Assert.That(await system.Permission.Find(app.filesystem.os.Combine("a").Absolute, Verb.write)).IsNotNull();
            await Assert.That(await user.Permission.Find(app.filesystem.os.Combine("a").Absolute, Verb.read)).IsNotNull();
            await Assert.That(await user.Permission.Find(app.filesystem.os.Combine("a").Absolute, Verb.write)).IsNull();
        }
    }

    // ── P4 sub app ─────────────────────────────────────────────────────────────

    [Test]
    [Arguments("System")]
    [Arguments("User")]
    public async Task P4_ASubAppsViews_StandOnlyOnItsOwnFolders_AndTheMakersViewsAreUnchanged(string maker)
    {
        await using var outer = new global::app.@this("/p4maker", global::PLang.Tests.Parent.Actor).Testing();
        var makerActor = maker == "System" ? outer.actor.list.System : outer.actor.list.User;
        var beforeSystem = Held(outer.actor.list.System);
        var beforeUser = Held(outer.actor.list.User);

        await using var sub = new global::app.@this("/p4maker/sub", makerActor);
        var subOs = sub.filesystem.os.Absolute;
        var subStore = sub.filesystem.store.Absolute;
        var makerStore = outer.filesystem.store.Absolute;

        // the sub app's System: the os (rwxd) and ITS OWN stores, nothing of the maker's
        await Assert.That(await Stands(sub.actor.list.System, subOs)).IsEqualTo("read, write, delete, execute, under read, under write, under delete, under execute");
        await Assert.That(await Stands(sub.actor.list.System, subStore)).IsEqualTo("read, write, delete, under read, under write, under delete");
        await Assert.That(await Stands(sub.actor.list.System, makerStore)).IsEqualTo("");
        await Assert.That(await Stands(sub.actor.list.System, "/p4maker/other")).IsEqualTo("");
        // the sub app's User: no store grant, not the maker's either
        await Assert.That(await Stands(sub.actor.list.User, subStore)).IsEqualTo("");
        await Assert.That(await Stands(sub.actor.list.User, makerStore)).IsEqualTo("");
        await Assert.That(await Stands(sub.actor.list.User, subOs)).IsEqualTo("read, execute, under read, under execute");

        // the maker's views did not move, and answer nothing of the sub app's stores
        await Assert.That(Held(outer.actor.list.System).SequenceEqual(beforeSystem)).IsTrue();
        await Assert.That(Held(outer.actor.list.User).SequenceEqual(beforeUser)).IsTrue();
        await Assert.That(await Stands(outer.actor.list.System, subStore)).IsEqualTo("");
        await Assert.That(await Stands(outer.actor.list.User, subStore)).IsEqualTo("");
    }

    [Test]
    public async Task P4_ASubAppRootedAtAnAncestor_DoesNotReachTheMakersStores_ThroughItsViews()
    {
        // observation: the maker lives at /p4anc/maker; a sub app rooted at its ANCESTOR /p4anc would have the maker's folder
        // in ITS root, so the maker's stores would be just in-root for it. The safe behaviour: the maker's .data is never
        // reachable by the sub app's actors — a sub app is made in its maker's folder or under it, so this one is refused
        // at its birth and has no actors at all.
        await using var outer = new global::app.@this("/p4anc/maker", global::PLang.Tests.Parent.Actor).Testing();

        var refused = await Assert.ThrowsAsync<global::app.error.AppException>(() =>
        {
            _ = new global::app.@this("/p4anc", outer.actor.list.User);
            return Task.CompletedTask;
        });

        await Assert.That(refused!.Error.Key).IsEqualTo("OutsideItsMaker");
        // a sibling of the maker's folder is outside it too; its own folder and one under it are in
        await Assert.That(() => new global::app.@this("/p4anc/other", outer.actor.list.User)).Throws<global::app.error.AppException>();
        await using var same = new global::app.@this("/p4anc/maker", outer.actor.list.User);
        await using var under = new global::app.@this("/p4anc/maker/sub", outer.actor.list.User);
    }

    // ── P5 name / role ─────────────────────────────────────────────────────────

    [Test]
    public async Task P5_AnActorsName_IsItsRoles_AndAViewMatchesOnlyItsOwnActorsGrants()
    {
        await using var app = new global::app.@this("/p5root", global::PLang.Tests.Parent.Actor).Testing();
        await Assert.That(app.actor.list.System.Name).IsEqualTo("System");
        await Assert.That(app.actor.list.User.Name).IsEqualTo("User");
        await Assert.That(Held(app.actor.list.System).Count).IsGreaterThan(0);

        // the grants a view stands carry its actor's name: a User-named request against the System's view finds nothing
        var system = app.actor.list.System.Permission;
        var osFile = app.filesystem.os.Combine("x").Absolute;
        await Assert.That(await system.Find(osFile, Verb.write)).IsNotNull();
        var field = typeof(global::app.actor.permission.@this).GetField("_standing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var names = ((System.Collections.IEnumerable)field.GetValue(system)!).Cast<global::app.data.@this>()
            .Select(d => ((Grant)d.Peek()!).Actor).Distinct().ToList();
        await Assert.That(names.Count).IsEqualTo(1);
        await Assert.That(names[0]).IsEqualTo("System");

        // a sub app's System grants are its own view's: the maker's System view does not answer for the sub's folders
        await using var sub = new global::app.@this("/p5root/sub", app.actor.list.System);
        await Assert.That(await app.actor.list.System.Permission.Find(sub.filesystem.store.Absolute + Sep + "x", Verb.read)).IsNull();
        await Assert.That(await sub.actor.list.System.Permission.Find(app.filesystem.store.Absolute + Sep + "x", Verb.read)).IsNull();
    }
}
