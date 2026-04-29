namespace PLang.Tests.App.DataTests;

// Contract tests for Data.As<T>(context) — the new resolution entry point in v4 Phase 2.
// v4 contract: As<T> walks _value, substitutes %var% via context.Variables.Get/Resolve, converts to T via TypeMapping,
//   and returns a fresh Data<T>. Every call resolves freshly. Data is stateless w.r.t. resolution.

public class DataAsTResolutionTests
{
    // this is Data<T> with correct typed Value already → As<T> returns this (fast path, no allocation).
    [Test] public async Task AsT_AlreadyTypedData_ReturnsSelf() => Assert.Fail("Not implemented");

    // Value is T already (boxed) but Data is not typed → As<T> wraps in fresh Data<T>, no TypeMapping call.
    [Test] public async Task AsT_ValueAlreadyT_FastPathWrap() => Assert.Fail("Not implemented");

    // Value is "%name%" (full match), Variables.Get("name").Value is T → returns Data<T> with that value.
    [Test] public async Task AsT_FullVarMatch_ReturnsVariableValue() => Assert.Fail("Not implemented");

    // Value is "%name%" but Variables doesn't have "name" → returns Data.FromError or NotFound (per contract).
    [Test] public async Task AsT_FullVarMatch_MissingVariable_ReturnsErrorOrNotFound() => Assert.Fail("Not implemented");

    // Value is "Hello %name%" (partial) → Variables.Resolve invoked, result cast to T.
    [Test] public async Task AsT_Interpolation_CallsResolve() => Assert.Fail("Not implemented");

    // Value is List<object?> with nested %var% strings → walks list, substitutes, converts to typed List<T>.
    [Test] public async Task AsT_ListWithNestedVars_DeepResolvesAndTypes() => Assert.Fail("Not implemented");

    // Value is Dictionary<string, object?> with %var% in values → walks, substitutes, converts to typed Dict.
    [Test] public async Task AsT_DictWithNestedVars_DeepResolvesAndTypes() => Assert.Fail("Not implemented");

    // T has static Resolve(string, Context) (e.g., FileSystem.Path) and Value is string → calls it.
    [Test] public async Task AsT_TypeWithStaticResolve_StringValue_DispatchesToResolve() => Assert.Fail("Not implemented");

    // TypeMapping conversion failure → Data.FromError with structured error message.
    [Test] public async Task AsT_ConversionFailure_ReturnsFromError() => Assert.Fail("Not implemented");

    // Two consecutive As<T> calls with the same context → walk runs twice, two fresh Data<T> instances (not cached on Data).
    [Test] public async Task AsT_CalledTwice_FreshResolutionEachCall() => Assert.Fail("Not implemented");

    // After first As<T>, original Data._value is unchanged (raw preserved).
    [Test] public async Task AsT_DoesNotMutateOriginalDataValue() => Assert.Fail("Not implemented");

    // List<Action.@this> elements pass through As<T> WITHOUT walking into Action (no recursion into sub-actions).
    [Test] public async Task AsT_ActionListElements_NotRecursedInto() => Assert.Fail("Not implemented");

    // Fresh context with different variable values → As<T> picks up the new values, no stale cache.
    [Test] public async Task AsT_DifferentContext_PicksUpFreshVariableValues() => Assert.Fail("Not implemented");
}
