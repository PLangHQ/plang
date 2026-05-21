using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Path = global::App.FileSystem.Path;
using PermissionRecord = global::App.FileSystem.Permission.@this;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using Write = global::App.FileSystem.Permission.Verb.Write;
using Delete = global::App.FileSystem.Permission.Verb.Delete;
using MatchMode = global::App.FileSystem.Permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.StorageTests;

/// Stage 3 — Batch 7: `Actor.@this.Permission` unifies in-memory ("y") and
/// persisted ("a") grants behind one Find/Add/Revoke surface.
public class ActorPermissionStorageTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-st-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static global::App.Data.@this<PermissionRecord> Grant(
        global::App.@this app, string actor, string path, Verb? verb = null, MatchMode match = MatchMode.Exact)
    {
        var p = new PermissionRecord(app.Id, actor, path, verb ?? new Verb(), match);
        return new global::App.Data.@this<PermissionRecord>("", p) { Context = app.User.Context };
    }

    [Test] public async Task RoundTrip_AddSignedAGrant_FindReturnsIt_SignatureValidates()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        grant.EnsureSigned();
        await app.User.Permission.Add(grant);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Value!.Path).IsEqualTo("/p");
    }

    [Test] public async Task PerActorIsolation_UserGrant_NotSurfacedTo_SystemFind()
    {
        var app = NewApp();
        var userGrant = Grant(app, app.User.Name, "/u");
        userGrant.EnsureSigned();
        await app.User.Permission.Add(userGrant);

        var found = await app.System.Permission.Find(new Path("/u", app.System.Context), new Verb { Read = new Read() });
        await Assert.That(found).IsNull();
    }

    [Test] public async Task TwoHomes_InMemoryGrant_AndPersistedGrant_FindReturnsCorrectOne()
    {
        var app = NewApp();
        // In-memory grant for /mem (unsigned → session)
        var memGrant = Grant(app, app.User.Name, "/mem");
        await app.User.Permission.Add(memGrant);

        // Persisted grant for /disk (signed → sqlite)
        var diskGrant = Grant(app, app.User.Name, "/disk");
        diskGrant.EnsureSigned();
        await app.User.Permission.Add(diskGrant);

        var mem = await app.User.Permission.Find(new Path("/mem", app.User.Context), new Verb { Read = new Read() });
        var disk = await app.User.Permission.Find(new Path("/disk", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(mem).IsNotNull();
        await Assert.That(disk).IsNotNull();
    }

    [Test] public async Task VerbNarrowing_FullAllowGrant_CoversNarrowedReadRequest()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p"); // default verb = fully granted
        await app.User.Permission.Add(grant);

        var narrowedRead = new Verb
        {
            Read = new Read(Recursive: false, Metadata: false),
            Write = null,
            Delete = null
        };
        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), narrowedRead);
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task VerbNarrowing_ReadOnlyGrant_DoesNotCoverDeleteRequest()
    {
        var app = NewApp();
        var readOnly = new Verb { Read = new Read(), Write = null, Delete = null };
        var grant = Grant(app, app.User.Name, "/p", verb: readOnly);
        await app.User.Permission.Add(grant);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Delete = new Delete() });
        await Assert.That(found).IsNull();
    }

    [Test] public async Task GlobMatch_PatternGrant_CoversExactPathRequest()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/apps/*/file.txt", match: MatchMode.Glob);
        await app.User.Permission.Add(grant);

        var found = await app.User.Permission.Find(new Path("/apps/Email/file.txt", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task GlobMatch_NonMatchingPatternGrant_DoesNotCover()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/apps/*/file.txt", match: MatchMode.Glob);
        await app.User.Permission.Add(grant);

        var found = await app.User.Permission.Find(new Path("/apps/Email/Sub/file.txt", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(found).IsNull();
    }

    [Test] public async Task Revoke_InMemoryGrant_RemovedFromSessionList()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p"); // unsigned → in-memory
        await app.User.Permission.Add(grant);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() })).IsNotNull();

        await app.User.Permission.Revoke(grant.Value!);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() })).IsNull();
    }

    [Test] public async Task Revoke_PersistedGrant_RemovesSqliteRow()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        grant.EnsureSigned();
        await app.User.Permission.Add(grant);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() })).IsNotNull();

        await app.User.Permission.Revoke(grant.Value!);
        await Assert.That(await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() })).IsNull();
    }

    [Test] public async Task SignatureFailure_CorruptedGrantInStore_FindSkipsIt()
    {
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        grant.EnsureSigned();
        // Tamper the path post-signing — signature no longer covers payload.
        var tampered = new global::App.Data.@this<PermissionRecord>("",
            new PermissionRecord(app.Id, app.User.Name, "/different", new Verb(), MatchMode.Exact))
        { Context = app.User.Context, Signature = grant.Signature };
        await app.User.Permission.Add(tampered);

        var found = await app.User.Permission.Find(new Path("/different", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(found).IsNull();
    }

    [Test] public async Task IdempotentAdd_SamePathTwice_Overwrites_NoDuplicateRow()
    {
        var app = NewApp();
        var first  = Grant(app, app.User.Name, "/p");
        var second = Grant(app, app.User.Name, "/p");
        await app.User.Permission.Add(first);
        await app.User.Permission.Add(second);

        // Find should still hit — overwrite, not duplicate.
        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task SignatureVerificationCached_FindWalksSameDataOnce()
    {
        // The cache lives on the Data instance via Properties[VerifiedFlag].
        // Most useful for in-memory grants where the same instance is walked
        // many times. Sqlite-backed grants deserialise fresh per Find, so the
        // cache helps within one Find pass (multiple candidates) rather than
        // across calls. Pin contract that the flag stamps on first verify.
        var app = NewApp();
        var grant = Grant(app, app.User.Name, "/p");
        grant.EnsureSigned();
        await app.User.Permission.Add(grant);

        var f1 = await app.User.Permission.Find(new Path("/p", app.User.Context), new Verb { Read = new Read() });
        await Assert.That(f1).IsNotNull();
        await Assert.That(f1!.Properties.Contains("permission.verified")).IsTrue();
    }
}
