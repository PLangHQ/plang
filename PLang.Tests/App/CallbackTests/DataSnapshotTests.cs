using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 3: `Data.@this.Snapshot` property and the
/// `action.Snapshot()` helper. Any action returning Exit-typed Data MUST attach
/// a non-null Snapshot — contract pinned by a generic invariant test.
public class DataSnapshotTests
{
    [Test] public Task DataSnapshot_PropertyExists_AndDefaultsNull()            { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task DataSnapshot_RoundTripsThrough_OkConstructor()           { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task DataSnapshot_SettableAfterConstruction()                 { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task ActionSnapshotHelper_ReturnsNonNull()                    { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ActionSnapshotHelper_MatchesContextAppSnapshot()         { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task ExitTypedData_MustCarry_NonNullSnapshot_Invariant()      { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
