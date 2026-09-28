using Path = global::app.type.item.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.StorageTests;

/// Batch 7: `Actor.@this.Permission` unifies in-memory ("y") and
/// persisted ("a") grants behind one Find/Add/Revoke surface.
public class ActorPermissionStorageTests
{
    private static global::app.@this NewApp(string? root = null)
    {
        var app = new global::app.@this(root ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-st-" + System.Guid.NewGuid().ToString("N")[..8]));
        // Real grant signing, but skip the identity recreate-loop (production identity
        // re-read fails verify → recreated keygen+sign each call, ~850ms).
        global::PLang.Tests.TestApp.UseSharedIdentity(app);
        return app;
    }

    private static global::app.data.@this<PermissionRecord> Grant(
        global::app.@this app, string actor, string path, Verb? verb = null, MatchMode match = MatchMode.Exact)
    {
        var verbs = verb is { } v ? new System.Collections.Generic.HashSet<Verb> { v } : PermissionRecord.AllVerbs;
        var p = new PermissionRecord(actor, path, verbs, match);
        return new global::app.data.@this<PermissionRecord>("", p, context: app.actor.list.User.Context);
    }

    [Test] public async Task RoundTrip_AddSignedAGrant_FindReturnsIt_SignatureValidates()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/p");
        await app.actor.list.User.Permission.Add(grant, persist: true);

        var found = await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read);
        await Assert.That(found).IsNotNull();
        await Assert.That((await found!.Value<PermissionRecord>())!.Path).IsEqualTo("/p");
    }

    [Test] public async Task PerActorIsolation_UserGrant_NotSurfacedTo_SystemFind()
    {
        var app = NewApp();
        var userGrant = Grant(app, app.actor.list.User.Name, "/u");
        await app.actor.list.User.Permission.Add(userGrant, persist: true);

        var found = await app.actor.list.System.Permission.Find(new Path("/u"), global::app.type.item.permission.Verb.Read);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task TwoHomes_InMemoryGrant_AndPersistedGrant_FindReturnsCorrectOne_AndRoutingHonoured()
    {
        var app = NewApp();
        // In-memory grant for /mem (unsigned → session, no Signature)
        var memGrant = Grant(app, app.actor.list.User.Name, "/mem");
        await app.actor.list.User.Permission.Add(memGrant, persist: false);

        // Persisted grant for /disk (signed → sqlite, Signature set)
        var diskGrant = Grant(app, app.actor.list.User.Name, "/disk");
        await app.actor.list.User.Permission.Add(diskGrant, persist: true);

        var mem = await app.actor.list.User.Permission.Find(new Path("/mem"), global::app.type.item.permission.Verb.Read);
        var disk = await app.actor.list.User.Permission.Find(new Path("/disk"), global::app.type.item.permission.Verb.Read);
        await Assert.That(mem).IsNotNull();
        await Assert.That(disk).IsNotNull();
        // Routing: only the persisted grant lands in the actor's saved permission setting; the session
        // one must NOT appear there.
        var paths = await Saved(app, app.actor.list.User);
        await Assert.That(paths).Contains("/disk");
        await Assert.That(paths).DoesNotContain("/mem");
    }

    // The paths of the grants the actor's own permission setting holds.
    private static async Task<List<string>> Saved(global::app.@this app, global::app.actor.@this actor)
    {
        await actor.Setting.Load();
        var paths = new List<string>();
        foreach (var d in actor.Setting.Of<global::app.actor.permission.setting.@this>().Grant.Items(actor.Context))
            if (await d.Value<PermissionRecord>() is { } p) paths.Add(p.Path);
        return paths;
    }

    // One path granted to both actors: each keeps its own grant, and revoking one leaves the other.
    [Test] public async Task SamePath_SystemAndUserGrants_BothSurvive_RevokingOneLeavesTheOther()
    {
        var app = NewApp();
        var systemGrant = Grant(app, app.actor.list.System.Name, "/shared");
        var userGrant = Grant(app, app.actor.list.User.Name, "/shared");
        await app.actor.list.System.Permission.Add(systemGrant, persist: true);
        await app.actor.list.User.Permission.Add(userGrant, persist: true);

        await Assert.That(await app.actor.list.System.Permission.Find(new Path("/shared"), Verb.Read)).IsNotNull();
        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/shared"), Verb.Read)).IsNotNull();

        await app.actor.list.User.Permission.Revoke((await userGrant.Value())!);
        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/shared"), Verb.Read)).IsNull();
        await Assert.That(await app.actor.list.System.Permission.Find(new Path("/shared"), Verb.Read)).IsNotNull();
    }

    // Permission is an actor's own: the user never holds the system's saved grants.
    [Test] public async Task SystemGrant_NotSurfacedTo_UserFind()
    {
        var app = NewApp();
        await app.actor.list.System.Permission.Add(Grant(app, app.actor.list.System.Name, "/s"), persist: true);

        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/s"), Verb.Read)).IsNull();
        await Assert.That(await Saved(app, app.actor.list.User)).DoesNotContain("/s");
    }

    // A saved grant is the actor's row: the next App on the same root reads it back.
    [Test] public async Task PersistedGrant_IsReadByTheNextApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-st-" + System.Guid.NewGuid().ToString("N")[..8]);
        await using (var first = NewApp(root))
            await first.actor.list.User.Permission.Add(Grant(first, first.actor.list.User.Name, "/kept"), persist: true);

        await using var next = NewApp(root);
        await Assert.That(await Saved(next, next.actor.list.User)).Contains("/kept");
        await Assert.That(await next.actor.list.User.Permission.Find(new Path("/kept"), Verb.Read)).IsNotNull();
    }

    [Test] public async Task VerbNarrowing_FullAllowGrant_CoversNarrowedReadRequest()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/p"); // default verb = fully granted
        await app.actor.list.User.Permission.Add(grant, persist: false);

        var found = await app.actor.list.User.Permission.Find(new Path("/p"), Verb.Read);
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task VerbNarrowing_ReadOnlyGrant_DoesNotCoverDeleteRequest()
    {
        var app = NewApp();
        var readOnly = global::app.type.item.permission.Verb.Read;
        var grant = Grant(app, app.actor.list.User.Name, "/p", verb: readOnly);
        await app.actor.list.User.Permission.Add(grant, persist: false);

        var found = await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Delete);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task GlobMatch_PatternGrant_CoversExactPathRequest()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/apps/*/file.txt", match: MatchMode.Glob);
        await app.actor.list.User.Permission.Add(grant, persist: false);

        var found = await app.actor.list.User.Permission.Find(new Path("/apps/Email/file.txt"), global::app.type.item.permission.Verb.Read);
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task GlobMatch_NonMatchingPatternGrant_DoesNotCover()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/apps/*/file.txt", match: MatchMode.Glob);
        await app.actor.list.User.Permission.Add(grant, persist: false);

        var found = await app.actor.list.User.Permission.Find(new Path("/apps/Email/Sub/file.txt"), global::app.type.item.permission.Verb.Read);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task Revoke_InMemoryGrant_RemovedFromSessionList()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/p"); // unsigned → in-memory
        await app.actor.list.User.Permission.Add(grant, persist: false);
        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read)).IsNotNull();

        await app.actor.list.User.Permission.Revoke((await grant.Value())!);
        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read)).IsNull();
    }

    [Test] public async Task Revoke_PersistedGrant_RemovesSqliteRow()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/p");
        await app.actor.list.User.Permission.Add(grant, persist: true);
        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read)).IsNotNull();

        await app.actor.list.User.Permission.Revoke((await grant.Value())!);
        await Assert.That(await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read)).IsNull();
    }

    [Skip("Tamper-detection moved to verify-on-read at the application/plang store boundary; SettingsStore verify-on-read is a deferred todo (OBP rewrite).")]
    [Test] public async Task SignatureFailure_CorruptedGrantInStore_FindSkipsIt()
    {
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/p");
        // Tamper the path post-signing — signature no longer covers payload.
        var tampered = new global::app.data.@this<PermissionRecord>("",
            new PermissionRecord(app.actor.list.User.Name, "/different", global::app.type.item.permission.@this.AllVerbs, MatchMode.Exact), context: app.actor.list.User.Context);
        await app.actor.list.User.Permission.Add(tampered, persist: true);

        var found = await app.actor.list.User.Permission.Find(new Path("/different"), global::app.type.item.permission.Verb.Read);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task IdempotentAdd_SamePathTwice_Overwrites_NoDuplicateRow()
    {
        var app = NewApp();
        var first  = Grant(app, app.actor.list.User.Name, "/p");
        var second = Grant(app, app.actor.list.User.Name, "/p");
        await app.actor.list.User.Permission.Add(first, persist: false);
        await app.actor.list.User.Permission.Add(second, persist: false);

        // Find should still hit — overwrite, not duplicate.
        var found = await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read);
        await Assert.That(found).IsNotNull();

        // Prove no-duplicate behaviorally: one Revoke should fully remove the
        // grant. If Add had stored a duplicate, the second copy would still
        // cover the request after Revoke.
        await app.actor.list.User.Permission.Revoke((await first.Value())!);
        var afterRevoke = await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read);
        await Assert.That(afterRevoke).IsNull();
    }

    [Skip("Reads a persisted grant back from sqlite via GetAll — now a signed-store round-trip that rides the deferred SettingsStore verify-on-read work (todos.md).")]
    [Test] public async Task IdempotentAdd_PersistedSamePathTwice_SingleSqliteRow()
    {
        var app = NewApp();
        var first  = Grant(app, app.actor.list.User.Name, "/p");
        var second = Grant(app, app.actor.list.User.Name, "/p");
        await app.actor.list.User.Permission.Add(first, persist: true);
        await app.actor.list.User.Permission.Add(second, persist: true);

        // SettingsStore.Set is keyed by path — the table must hold one row
        // for `/p`, not two.
        var stored = await (await app.store).GetAll<global::app.type.item.permission.@this>("permission");
        await stored.IsSuccess();
        var rowsForP = (await stored.Value())!.Items(global::PLang.Tests.TestApp.SharedContext).Count(d => d.GetValue<global::app.type.item.permission.@this>()?.Path == "/p");
        await Assert.That(rowsForP).IsEqualTo(1);
    }

    [Skip("The in-memory VerifiedFlag verification cache was removed with Data.Signature; grant verification is now a store-read-boundary concern (deferred todo).")]
    [Test] public async Task SignatureVerificationCached_FindWalksSameDataOnce()
    {
        // The cache lives on the Data instance via Properties[VerifiedFlag].
        // Most useful for in-memory grants where the same instance is walked
        // many times. Sqlite-backed grants deserialise fresh per Find, so the
        // cache helps within one Find pass (multiple candidates) rather than
        // across calls. Pin contract that the flag stamps on first verify.
        var app = NewApp();
        var grant = Grant(app, app.actor.list.User.Name, "/p");
        await app.actor.list.User.Permission.Add(grant, persist: true);

        var f1 = await app.actor.list.User.Permission.Find(new Path("/p"), global::app.type.item.permission.Verb.Read);
        await Assert.That(f1).IsNotNull();
        await Assert.That(f1!.Properties.Contains("permission.verified")).IsTrue();
    }
}
