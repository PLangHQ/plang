namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 4
// Properties get a wire scope: C# type becomes Dictionary<string, object?> of primitives;
// the wire emits them as a nested `properties` object next to name/type/value/signature.
// Keys are unconstrained because they live inside a scope (no collision with reserved
// top-level fields).
// Coverage matrix rows 4.1–4.8, 4.15–4.18.

public class PropertiesWireShapeTests
{
    // 4.1 — Properties is IDictionary<string, object?> (or a dictionary-backed wrapper).
    [Test] public async Task Properties_Surface_IsDictionaryStringObject_NotIListData()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.2 — Supported primitives round-trip through application/plang.
    [Test] public async Task Properties_RoundTrip_StringPrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_RoundTrip_IntPrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_RoundTrip_LongPrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_RoundTrip_DoublePrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_RoundTrip_BoolPrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_RoundTrip_DateTimePrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_RoundTrip_ByteArrayPrimitive()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.3 — Nested dict of primitives round-trips.
    [Test] public async Task Properties_RoundTrip_NestedDictOfPrimitives()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.4 — List of primitives round-trips.
    [Test] public async Task Properties_RoundTrip_ListOfPrimitives()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.5 — Wire JSON of a Data with Properties["cost"]=100 emits properties as a
    //        nested object sibling of name/type/value/signature, NOT a top-level `cost`.
    [Test] public async Task Wire_PropertiesEmittedAsNestedObject_SiblingOfReservedFields()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Wire_PropertyKey_DoesNotLeakToRootLevel()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.6 — Empty Properties → the `properties` field is omitted entirely from the wire.
    [Test] public async Task Wire_EmptyProperties_OmitsPropertiesFieldEntirely()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.7 — Reserved-name keys are fine inside the nested `properties` object: no throw.
    [Test] public async Task Properties_KeyNamedValue_RoundTripsIntact()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_KeyNamedSignature_RoundTripsIntact()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Properties_KeyNamedName_RoundTripsIntact()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.8 — JSON int → long promotion on read (matches JsonElement.GetInt64 contract).
    [Test] public async Task Properties_IntValue_ReadBackAsLong_JsonPromotion()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.15 — Mutating any byte inside the wire `properties` object (re-encode without
    //        re-signing) fails outer signature verification.
    [Test] public async Task OuterSignature_AfterPropertiesValueTamper_FailsVerify()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.16 — Sign-if-missing converter does NOT visit Properties values as Data nodes.
    //        Properties values are primitives; no inner signatures appear under `properties`.
    [Test] public async Task Wire_PropertiesValues_HaveNoNestedSignatures()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.17 — Unknown top-level field on read is silently ignored (default STJ behaviour).
    //        The wire emits the five reserved fields; an extra `traceId:"..."` is dropped.
    [Test] public async Task WireRead_UnknownTopLevelField_SilentlyIgnored_NotCapturedAsProperty()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 4.18 — Old IList<Data> by-int indexer is gone (compiler-guided migration of callers).
    [Test] public async Task Properties_OldIListByIntIndexer_NoLongerExists()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
