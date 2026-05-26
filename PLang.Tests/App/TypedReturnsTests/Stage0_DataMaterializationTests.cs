namespace PLang.Tests.App.TypedReturnsTests;

// Stage 0 — Data materialization owned by .Type (A.4).
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 0, item 6)
// Plan: .bot/typed-action-returns/architect/plan.md (A.4)

public class Stage0_DataMaterializationTests
{
    [Test]
    public async Task Data_GenericAsT_DoesNotExistAsPublicApi()
        // Reflection: Data has no public method `As<T>()`. The generic shape leaks the materializer choice to the call site.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Data_AsString_LooksUpMaterializerByTypeName()
        // Data{Value="{\"a\":1}", Type="json"}.As("json") returns a JsonNode parsed from Value.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Data_PropertyAccess_UsesDeclaredTypeForMaterialization()
        // Data tagged Type="json" with raw string Value; property dereference (%var.a%) materializes once.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Data_Materialization_CachesResultOnFirstAccess()
        // Repeated property access does not re-run the materializer (observe via a counting serializer).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Data_VariableSet_NoParsingAtSetTime()
        // variable.set with Type="csv" stores Value untouched (no I/O, no parse) — verify via byte equality with input.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Data_AsString_UnknownType_SurfacesErrorAtAccess_NotAtSet()
        // Data tagged Type="bogus" sets cleanly; .As("bogus") or property access surfaces a clear error.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Data_AsString_CrossTypeCoercion_LooksUpRequestedNotDeclared()
        // Data Type="csv" but caller .As("json") → uses the json materializer (explicit coercion, rare path).
        => Assert.Fail("Not implemented");
}
