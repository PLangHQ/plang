namespace PLang.Tests.App;

// Contract tests for Action.GetParameter(name, context) — the new lookup method introduced in v4 Phase 1.
// Lives at PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs.
// v4 contract: walks Parameters, falls back to Defaults, returns Data.NotFound. Pure lookup, no resolution.

public class GetParameterTests
{
    // Parameter present in Parameters → returns the same Data instance (reference equality, not a copy).
    [Test] public async Task GetParameter_FoundInParameters_ReturnsSameDataInstance() => Assert.Fail("Not implemented");

    // Parameter absent from Parameters but present in Defaults → returns the Defaults entry.
    [Test] public async Task GetParameter_FallsBackToDefaults_WhenNotInParameters() => Assert.Fail("Not implemented");

    // Parameter absent from both → returns Data.NotFound (IsInitialized = false).
    [Test] public async Task GetParameter_NotFound_ReturnsDataNotFound() => Assert.Fail("Not implemented");

    // Lookup is case-sensitive on Name (matches today's Parameters lookup behavior).
    [Test] public async Task GetParameter_CaseSensitive_DistinctNamesNotMatched() => Assert.Fail("Not implemented");

    // GetParameter does NOT trigger resolution — returned Data.Value is whatever construction set (raw).
    [Test] public async Task GetParameter_NoResolutionSideEffect_ValueRemainsRaw() => Assert.Fail("Not implemented");

    // Empty Parameters list, empty Defaults → returns Data.NotFound, not null, not exception.
    [Test] public async Task GetParameter_EmptyLists_ReturnsNotFound() => Assert.Fail("Not implemented");
}
