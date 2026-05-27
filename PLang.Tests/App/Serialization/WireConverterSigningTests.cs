namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Sign-if-missing during the wire converter walk. Each Data the converter visits during
// serialization: if Signature is null, call EnsureSigned; if populated, leave alone.
// The unit of attestation is the Data node, not the wire boundary.
// Coverage matrix rows 2.6, 2.7, 2.8, 2.13, 2.14. Plus byte[] leaf handling — added by
// test-designer because Stage 3's flat Compress depends on it.

public class WireConverterSigningTests
{
    // 2.6 — Unsigned Data → converter calls EnsureSigned, emits the populated signature.
    [Test] public async Task WireConverter_OnUnsignedData_FiresEnsureSignedAndEmitsSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.7 — Already-signed Data → converter leaves the signature unchanged (idempotent).
    [Test] public async Task WireConverter_OnSignedData_LeavesSignatureUnchanged()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.7b — EnsureSigned called twice in succession is a no-op on the second call.
    [Test] public async Task EnsureSigned_CalledTwice_DoesNotProduceTwoSignatures()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.8 — Each List<Data> element inside Value gets its own signature on the wire.
    [Test] public async Task WireConverter_OnListDataInsideValue_SignsEachElement()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.13 — A channel BeforeWrite handler that mutates Data has its mutation included
    //        in the signature canonicalization (handler runs before converter signs).
    [Test] public async Task BeforeWriteHandler_MutatesData_MutationIncludedInCanonicalSign()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.14 — Reading via application/plang populates Signature without auto-verifying.
    //        Verification is an explicit signing.verify step.
    [Test] public async Task ApplicationPlang_Read_PopulatesSignature_WithoutAutoVerify()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // (Test-designer addition.) A Data whose Value is a raw byte[] emits the bytes
    // through the wire converter without wrapping in a nested Data — this is the
    // shape Compress relies on after Stage 3 flattens.
    [Test] public async Task WireConverter_OnByteArrayValue_EmitsBytesWithoutNestedDataWrap()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Properties skip — converter walks Value graph only, never Properties.
    // (Cross-referenced under PropertiesWireShapeTests; pinned here too because the
    // discipline belongs to the wire converter.)
    [Test] public async Task WireConverter_DoesNotWalkProperties_AsDataNodes()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
