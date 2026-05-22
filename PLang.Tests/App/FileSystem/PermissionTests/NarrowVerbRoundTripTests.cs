using Path = global::app.types.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PermissionRecord = global::app.types.path.permission.@this;
using Verb = global::app.types.path.permission.verb.@this;
using Read = global::app.types.path.permission.verb.Read;
using MatchMode = global::app.types.path.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Repro for the Authorize → Find round-trip. Pre-Verb-narrowing tests passed
/// because both grant and request carried wide Verbs. After narrowing the
/// request side, the grant-side stored a narrow Verb too and the test's
/// Find request with explicit nulls should still find the grant.
public class NarrowVerbRoundTripTests
{
    [Test] public async Task NarrowRead_AddDirect_FindDirect_InMemory_Found()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-nv-" + System.Guid.NewGuid().ToString("N")[..8]));
        var narrowRead = new Verb { Read = new Read(), Write = null, Delete = null };
        var perm = new PermissionRecord(app.User.Name, "/p", narrowRead, MatchMode.Exact);
        var grant = new global::app.data.@this<PermissionRecord>("", perm) { Context = app.User.Context };
        // No EnsureSigned → in-memory path.
        await app.User.Permission.Add(grant);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), narrowRead);
        await Assert.That(found).IsNotNull();
    }

    [Test] public async Task NarrowRead_AddDirect_FindDirect_Sqlite_Found()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-nv-" + System.Guid.NewGuid().ToString("N")[..8]));
        var narrowRead = new Verb { Read = new Read(), Write = null, Delete = null };
        var perm = new PermissionRecord(app.User.Name, "/p", narrowRead, MatchMode.Exact);
        var grant = new global::app.data.@this<PermissionRecord>("", perm) { Context = app.User.Context };
        grant.EnsureSigned();  // → sqlite path
        await app.User.Permission.Add(grant);

        var found = await app.User.Permission.Find(new Path("/p", app.User.Context), narrowRead);
        await Assert.That(found).IsNotNull();
    }
}
