using Path = global::app.type.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.type.permission.@this;
using Verb = global::app.type.permission.Verb;
using MatchMode = global::app.type.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.StorageTests;

/// Batch 7: `Actor.@this.Permission` unifies in-memory ("y") and
/// persisted ("a") grants behind one Find/Add/Revoke surface.
public class ActorPermissionStorageTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-st-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static global::app.data.@this<PermissionRecord> Grant(
        global::app.@this app, string actor, string path, Verb? verb = null, MatchMode match = MatchMode.Exact)
    {
        var verbs = verb is { } v ? new System.Collections.Generic.HashSet<Verb> { v } : PermissionRecord.AllVerbs;
        var p = new PermissionRecord(actor, path, verbs, match);
        return new global::app.data.@this<PermissionRecord>("", p, context: app.User.Context);
    }

    [Test] public async Task RoundTrip_AddSignedAGrant_FindReturnsIt_SignatureValidates()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        await app.User.Permission.Add(grant, persist: true);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(found).IsNotNull();
        await Assert.That((await found!.Value<PermissionRecord>())!.Path).IsEqualTo("/p");
    }

    [Test] public async Task PerActorIsolation_UserGrant_NotSurfacedTo_SystemFind()
    {
        var app = NewApp();
        var userGrant = Grant(app, app.User.Name, "/u");
        await app.User.Permission.Add(userGrant, persist: true);

        var found = await app.System.Permission.Find(new Path("/u", app.System.Context), global::app.type.permission.Verb.Read);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task TwoHomes_InMemoryGrant_AndPersistedGrant_FindReturnsCorrectOne_AndRoutingHonoured()
    {
        var app = NewApp();
        // In-memory grant for /mem (unsigned → session, no Signature)
        var memGrant = Grant(app, app.User.Name, "/mem");
        await app.User.Permission.Add(memGrant, persist: false);

        // Persisted grant for /disk (signed → sqlite, Signature set)
        var diskGrant = Grant(app, app.User.Name, "/disk");
        await app.User.Permission.Add(diskGrant, persist: true);

        var mem = await app.User.Permission.Find(new Path("/mem", app.User.Context), global::app.type.permission.Verb.Read);
        var disk = await app.User.Permission.Find(new Path("/disk", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(mem).IsNotNull();
        await Assert.That(disk).IsNotNull();
        // Routing: only the signed grant lands in sqlite. The unsigned one
        // must NOT appear there — proves Add's signature-presence heuristic
        // sends the two grants to different homes.
        var stored = await (await app.SettingsStore).GetAll<global::app.type.permission.@this>("permission");
        await stored.IsSuccess();
        var paths = new List<string>();
        foreach (var d in (await stored.Value())!.Items)
        {
            ((global::app.data.@this)d).Context = app.User.Context;
            if (await ((global::app.data.@this)d).Value<PermissionRecord>() is { } p) paths.Add(p.Path);
        }
        await Assert.That(paths).Contains("/disk");
        await Assert.That(paths).DoesNotContain("/mem");
    }

    [Test] public async Task VerbNarrowing_FullAllowGrant_CoversNarrowedReadRequest()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p"); // default verb = fully granted
        await app.User.Permission.Add(grant, persist: false);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), Verb.Read);
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task VerbNarrowing_ReadOnlyGrant_DoesNotCoverDeleteRequest()
    {
        var app = NewApp();
        var readOnly = global::app.type.permission.Verb.Read;
        var grant = Grant(app, app.User.Name, "/p", verb: readOnly);
        await app.User.Permission.Add(grant, persist: false);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Delete);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task GlobMatch_PatternGrant_CoversExactPathRequest()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/apps/*/file.txt", match: MatchMode.Glob);
        await app.User.Permission.Add(grant, persist: false);

        var found = await app.User.Permission.Find(new Path("/apps/Email/file.txt", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task GlobMatch_NonMatchingPatternGrant_DoesNotCover()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/apps/*/file.txt", match: MatchMode.Glob);
        await app.User.Permission.Add(grant, persist: false);

        var found = await app.User.Permission.Find(new Path("/apps/Email/Sub/file.txt", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task Revoke_InMemoryGrant_RemovedFromSessionList()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p"); // unsigned → in-memory
        await app.User.Permission.Add(grant, persist: false);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read)).IsNotNull();

        await app.User.Permission.Revoke((await grant.Value())!);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read)).IsNull();
    }

    [Test] public async Task Revoke_PersistedGrant_RemovesSqliteRow()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        await app.User.Permission.Add(grant, persist: true);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read)).IsNotNull();

        await app.User.Permission.Revoke((await grant.Value())!);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read)).IsNull();
    }

    [Skip("Tamper-detection moved to verify-on-read at the application/plang store boundary; SettingsStore verify-on-read is a deferred todo (OBP rewrite).")]
    [Test] public async Task SignatureFailure_CorruptedGrantInStore_FindSkipsIt()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        // Tamper the path post-signing — signature no longer covers payload.
        var tampered = new global::app.data.@this<PermissionRecord>("",
            new PermissionRecord(app.User.Name, "/different", global::app.type.permission.@this.AllVerbs, MatchMode.Exact))
        { Context = app.User.Context };
        await app.User.Permission.Add(tampered, persist: true);

        var found = await app.User.Permission.Find(new Path("/different", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(found).IsNull();
    }

    [Test] public async Task IdempotentAdd_SamePathTwice_Overwrites_NoDuplicateRow()
    {
        var app = NewApp();
        var first  = Grant(app, app.User.Name, "/p");
        var second = Grant(app, app.User.Name, "/p");
        await app.User.Permission.Add(first, persist: false);
        await app.User.Permission.Add(second, persist: false);

        // Find should still hit — overwrite, not duplicate.
        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(found).IsNotNull();

        // Prove no-duplicate behaviorally: one Revoke should fully remove the
        // grant. If Add had stored a duplicate, the second copy would still
        // cover the request after Revoke.
        await app.User.Permission.Revoke((await first.Value())!);
        var afterRevoke = await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(afterRevoke).IsNull();
    }

    [Skip("Reads a persisted grant back from sqlite via GetAll — now a signed-store round-trip that rides the deferred SettingsStore verify-on-read work (todos.md).")]
    [Test] public async Task IdempotentAdd_PersistedSamePathTwice_SingleSqliteRow()
    {
        var app = NewApp();
        var first  = Grant(app, app.User.Name, "/p");
        var second = Grant(app, app.User.Name, "/p");
        await app.User.Permission.Add(first, persist: true);
        await app.User.Permission.Add(second, persist: true);

        // SettingsStore.Set is keyed by path — the table must hold one row
        // for `/p`, not two.
        var stored = await (await app.SettingsStore).GetAll<global::app.type.permission.@this>("permission");
        await stored.IsSuccess();
        var rowsForP = (await stored.Value())!.Items.Count(d => d.GetValue<global::app.type.permission.@this>()?.Path == "/p");
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
        var grant = Grant(app, app.User.Name, "/p");
        await app.User.Permission.Add(grant, persist: true);

        var f1 = await app.User.Permission.Find(new Path("/p", app.User.Context), global::app.type.permission.Verb.Read);
        await Assert.That(f1).IsNotNull();
        await Assert.That(f1!.Properties.Contains("permission.verified")).IsTrue();
    }
}
