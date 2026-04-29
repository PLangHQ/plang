namespace PLang.Tests.Generator.Matrix.Snapshot;

// Matrix entry for __SnapshotParams diagnostic.
// v4 contract: each property contributes one snapshot entry — PrValue (raw, via .Value), FinalValue (post-As<T>, via backing field).
// Snapshot is attached to errors via ParamSnapshot/Error.Params.

public class SnapshotOnErrorTests
{
    // Handler errors mid-Run() → Error.Params contains every parameter property's PrValue (raw) and FinalValue (if accessed).
    [Test] public async Task SnapshotOnError_ErrorMidRun_AttachesParamsToError() => Assert.Fail("Not implemented");

    // Property that was accessed before the error → its FinalValue is the resolved Data<T>.Value.
    [Test] public async Task SnapshotOnError_AccessedProperty_FinalValuePresent() => Assert.Fail("Not implemented");

    // Property that was NOT accessed → still appears in snapshot with PrValue, but FinalValue is null.
    [Test] public async Task SnapshotOnError_UnaccessedProperty_FinalValueNull() => Assert.Fail("Not implemented");

    // PrValue is the raw Parameter Data.Value (no resolution) — verifies v4's "trivially clean" snapshot impl.
    [Test] public async Task SnapshotOnError_PrValueIsRaw_NoResolution() => Assert.Fail("Not implemented");

    // No error path → no snapshot attached (snapshot is error-time-only diagnostic).
    [Test] public async Task SnapshotOnError_HandlerSucceeds_NoSnapshotAttached() => Assert.Fail("Not implemented");
}
