namespace PLang.Tests.Generator;

// Contract tests for __SnapshotParams generator emission (v4 simplification).
// v4 contract: per-property snapshot entry is trivial because Data is now stateless w.r.t. resolution:
//   PrValue = __action.GetParameter(name, context).Value   (raw, no work)
//   FinalValue = __<Name>_backing?.Value                   (post-As<T>, if accessed)
// Snapshot is attached to errors via ParamSnapshot/Error.Params.
// Phase 4 moves snapshot emission into ActionProperty.EmitSnapshotEntry (per-property contribution).

public class SnapshotParamsTests
{
    // Generator emits one __SnapshotParams entry per property — count matches the parameter properties on the handler.
    [Test] public async Task SnapshotParams_OneEntryPerProperty() => Assert.Fail("Not implemented");

    // Each entry captures PrValue from __action.GetParameter(name, context).Value (raw).
    [Test] public async Task SnapshotEntry_PrValue_FromGetParameterValue() => Assert.Fail("Not implemented");

    // Each entry captures FinalValue from the backing field's .Value (or null if backing field is null).
    [Test] public async Task SnapshotEntry_FinalValue_FromBackingFieldValue() => Assert.Fail("Not implemented");

    // Property never accessed during Run() → backing field is null → FinalValue is null in snapshot, PrValue still present.
    [Test] public async Task SnapshotEntry_UnaccessedProperty_PrValueOnly() => Assert.Fail("Not implemented");

    // Property accessed and resolved → both PrValue (raw) and FinalValue (resolved) present, distinct values.
    [Test] public async Task SnapshotEntry_AccessedProperty_BothPresent_Distinct() => Assert.Fail("Not implemented");

    // Snapshot attached to Error.Params when handler returns Data.FromError or throws.
    [Test] public async Task Snapshot_AttachedToError_OnFailure() => Assert.Fail("Not implemented");

    // No snapshot attached on successful return — snapshot is error-time-only.
    [Test] public async Task Snapshot_NotAttached_OnSuccess() => Assert.Fail("Not implemented");

    // ActionProperty.EmitSnapshotEntry produces the expected fragment per property kind (DataProperty + ProviderProperty).
    [Test] public async Task EmitSnapshotEntry_PerPropertyKind_ProducesExpectedFragment() => Assert.Fail("Not implemented");
}
