namespace PLang.Tests.Generator.Matrix.DataWrapped;

// Matrix entries for Data<T> properties whose Parameter Value contains %var% references
// (the "wrapped" shapes from .pr deserialization: scalar with %var%, list with nested %var%, dict with nested %var%).
//
// v4 contract: As<T> walks _value, substitutes %var% via Context.Variables.Get/Resolve, converts to T via TypeMapping.
// As<T> does NOT walk into Action.@this elements (sub-actions retain raw %var% — replaces today's IsDeferredActionTemplate carve-out).

public class DataWrappedStringTests
{
    // Parameter Value "%greeting%" (full %var% match) → As<string> calls Variables.Get("greeting").Value → typed Data<string>.
    [Test] public async Task DataWrappedString_FullVarMatch_ResolvesToVariableValue() => Assert.Fail("Not implemented");

    // Parameter Value "Hello %name%" (interpolation) → As<string> calls Variables.Resolve(str, ctx) → typed Data<string>.
    [Test] public async Task DataWrappedString_Interpolation_ResolvesViaResolve() => Assert.Fail("Not implemented");

    // Variable referenced is missing → As<string> returns Data.FromError (or empty per Resolve semantics).
    [Test] public async Task DataWrappedString_MissingVariable_HandlesGracefully() => Assert.Fail("Not implemented");
}

public class DataWrappedListTests
{
    // Parameter Value List<object?> { Dict { Content = "%comment%" } } → As<List<LlmMessage>> walks list, substitutes %comment% in inner dict, converts to typed list.
    [Test] public async Task DataWrappedList_NestedVarInDict_DeepResolvesAndTypes() => Assert.Fail("Not implemented");

    // Empty list parameter → As<List<LlmMessage>> returns empty typed list, no error.
    [Test] public async Task DataWrappedList_EmptyList_ReturnsEmptyTyped() => Assert.Fail("Not implemented");
}

public class DataWrappedDictTests
{
    // Parameter Value Dictionary { "Inner" = "%x%" } → As<Dictionary<string,object>> walks, substitutes, returns typed dict.
    [Test] public async Task DataWrappedDict_NestedVar_DeepResolves() => Assert.Fail("Not implemented");
}

public class DataWrappedActionListTests
{
    // Parameter Value List<Action.@this> (sub-actions for a deferred call) → As<List<Action>> does NOT walk into Action elements.
    // Verifies replacement of the IsDeferredActionTemplate carve-out: Action.@this items pass through with their raw %var% intact.
    [Test] public async Task DataWrappedActionList_DoesNotRecurseIntoActions() => Assert.Fail("Not implemented");

    // Sub-actions retain their own Parameters with raw %var% Values → no premature resolution.
    [Test] public async Task DataWrappedActionList_SubActionParametersRemainRaw() => Assert.Fail("Not implemented");
}
