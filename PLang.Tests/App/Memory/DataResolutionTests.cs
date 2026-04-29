namespace PLang.Tests.App.Memory;

// REWRITTEN FOR v4 (was: snapshot-once .Value semantics).
// v4 contract: resolution lives in Data.As<T>(context), not Data.Value's getter.
//   - .Value is raw (see DataValueRawTests).
//   - As<T> is fresh every call (see DataAsTResolutionTests).
//   - This file holds the integration-level resolution tests that exercise both pieces together —
//     scenarios that span construction → flow-through → As<T> → handler-side caching.
//
// The file's previous tests (snapshot-once on .Value, NeedsResolution gating, ResetResolution roundtrip)
// asserted the OPPOSITE contract and have been deleted. The replacements assert that:
//   1. The same raw Parameter Data, exercised by two action executions with different variable values,
//      yields two different As<T> results (because resolution is fresh per call).
//   2. The handler's backing-field cache is the ONLY cache; resetting it (per ExecuteAsync) is what
//      lets %var% updates flow through to subsequent reads.
//   3. Data is stateless w.r.t. resolution — sharing a Data instance across actions is safe.

public class DataResolutionTests
{
    // Same Parameter Data, two As<string> calls with different Variables.Get("x") between → two different results.
    [Test] public async Task SharedParameterData_AsTBetweenChanges_YieldsTwoResults() => Assert.Fail("Not implemented");

    // After As<T>, original Data._value is byte-for-byte the same as before — no in-place mutation.
    [Test] public async Task AsT_DoesNotMutateOriginalRaw() => Assert.Fail("Not implemented");

    // Loop iteration scenario: action runs N times, %i% changes each iteration → property reads N distinct values.
    [Test] public async Task LoopIteration_PropertyResolvesPerCall() => Assert.Fail("Not implemented");

    // Sub-goal scenario: parent goal's parameter Data is read by parent, then by sub-goal with different vars →
    //   each goal sees its own resolved view; raw Data is unchanged.
    [Test] public async Task SubGoalCall_EachGoalSeesOwnResolvedView() => Assert.Fail("Not implemented");

    // Variables.Get returns existing Data → As<T> on a parameter referencing that variable returns its Value cleanly.
    [Test] public async Task FullVarMatch_VariableHoldsData_UnwrappedCleanly() => Assert.Fail("Not implemented");

    // List<LlmMessage> with nested %comment% → first call resolves to "value1", set %comment%="value2", second call resolves to "value2".
    [Test] public async Task DeepResolution_ListWithVar_RefreshesPerCall() => Assert.Fail("Not implemented");

    // Concurrent As<T> calls on the same parameter Data → no race, two valid (independent) results.
    [Test] public async Task ConcurrentAsT_OnSharedParameterData_NoRace() => Assert.Fail("Not implemented");
}
