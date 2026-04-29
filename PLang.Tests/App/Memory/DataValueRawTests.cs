namespace PLang.Tests.App.Memory;

// Contract tests for v4's central architectural sharpening:
// Data.Value is RAW — read-only post-construction, no side effects, no resolution, no caching.
// Data flows through the system unchanged at the .Value level; resolution lives in As<T>(context).
//
// These tests directly probe the "Data flows through, no unwrapping" big picture:
//   - .Value never does work
//   - Data is stateless w.r.t. resolution (no _resolved, no ResetResolution, no IsDeferredActionTemplate)
//   - The same raw Data instance can be read N times across N action executions without mutation
//
// Phase 2 deletes these flags + methods from Data.@this. Tests fail today (gates exist) and pass after Phase 2.

public class DataValueRawTests
{
    // .Value returns whatever the constructor set — no transformation, no Variables lookup.
    [Test] public async Task Value_AfterConstruction_ReturnsRawAsSet() => Assert.Fail("Not implemented");

    // Reading .Value 1000 times → no work performed; same reference returned every time.
    [Test] public async Task Value_ReadRepeatedly_NoSideEffects() => Assert.Fail("Not implemented");

    // .Value on a string with "%var%" content → returns the literal "%var%" string, NOT a substituted value.
    // (This is the v4 contract change: today this triggers resolution; after v4 it does not.)
    [Test] public async Task Value_StringWithVarPlaceholder_ReturnsRawNotSubstituted() => Assert.Fail("Not implemented");

    // .Value on a List<object?> with nested %var% items → returns the original list reference, items unchanged.
    [Test] public async Task Value_ListWithVarPlaceholders_ReturnsRawListUnchanged() => Assert.Fail("Not implemented");

    // .Value on a Dictionary with nested %var% values → returns original dict, no substitution.
    [Test] public async Task Value_DictWithVarPlaceholders_ReturnsRawDictUnchanged() => Assert.Fail("Not implemented");

    // .Value reads do NOT depend on Context/Variables — even with no context attached, .Value works.
    [Test] public async Task Value_NoContextAttached_StillReadable() => Assert.Fail("Not implemented");

    // After v4: Data has no _resolved field. (Compile-time assertion via reflection — guards against re-introduction.)
    [Test] public async Task Data_HasNoResolvedField_AfterV4() => Assert.Fail("Not implemented");

    // After v4: Data has no ResetResolution method.
    [Test] public async Task Data_HasNoResetResolutionMethod_AfterV4() => Assert.Fail("Not implemented");

    // After v4: Data has no IsDeferredActionTemplate gate.
    [Test] public async Task Data_HasNoIsDeferredActionTemplateGate_AfterV4() => Assert.Fail("Not implemented");

    // Data flows through Action.GetParameter unchanged — the same Data instance is returned (reference equality).
    [Test] public async Task DataFlow_ThroughGetParameter_ReferenceIdentityPreserved() => Assert.Fail("Not implemented");

    // Two parallel readers of the same Data → no race, no shared mutation, same .Value reference.
    [Test] public async Task Value_ConcurrentReads_NoRace() => Assert.Fail("Not implemented");
}
