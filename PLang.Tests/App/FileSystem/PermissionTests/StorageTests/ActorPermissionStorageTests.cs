using TUnit.Core;

namespace PLang.Tests.App.FileSystem.PermissionTests.StorageTests;

/// Stage 3 — Batch 7: `Actor.@this.Permission` unifies in-memory ("y") and
/// persisted ("a") grants behind one Find/Add/Revoke surface. Per-actor
/// scoping via `json_extract` filter on a shared `permission` table.
public class ActorPermissionStorageTests
{
    [Test] public Task RoundTrip_AddSignedAGrant_FindReturnsIt_SignatureValidates() { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task PerActorIsolation_UserGrant_NotSurfacedTo_SystemFind()   { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task TwoHomes_InMemoryGrant_AndPersistedGrant_FindReturnsCorrectOne() { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task VerbNarrowing_FullAllowGrant_CoversNarrowedReadRequest() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task VerbNarrowing_ReadOnlyGrant_DoesNotCoverDeleteRequest()  { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task GlobMatch_PatternGrant_CoversExactPathRequest()          { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task GlobMatch_NonMatchingPatternGrant_DoesNotCover()         { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task Revoke_InMemoryGrant_RemovedFromSessionList()            { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Revoke_PersistedGrant_RemovesSqliteRow()                 { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task SignatureFailure_CorruptedGrantInStore_FindSkipsIt()     { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task IdempotentAdd_SamePathTwice_Overwrites_NoDuplicateRow()  { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task SignatureVerificationCached_FindWalksSameDataOnce()      { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
