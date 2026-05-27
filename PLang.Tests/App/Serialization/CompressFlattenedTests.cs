namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 3
// Compress / Decompress are flattened: no more `Data{archived, Data{gzip, byte[]}}`.
// The inner gzip Data was redundant — archived.Value can be byte[] directly.
// "Data all the way down" is a property of *byte layers* (decompress reveals another
// serialized Data), not of JSON object nesting.
// Coverage matrix rows 3.1–3.3, 3.5–3.8.

public class CompressFlattenedTests
{
    // 3.1 — Compress(D1) produces Data { type=archived, value=byte[] }, no nested gzip Data.
    [Test] public async Task Compress_OnSimpleData_ProducesArchivedTypeWithByteArrayValue()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.1b — No nested Data wrapper around the gzip payload (the smell this stage fixes).
    [Test] public async Task Compress_OnSimpleData_ValueIsRawByteArray_NotWrappedInData()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.2 — Decompress(Compress(D)) round-trips name and value.
    [Test] public async Task Decompress_AfterCompress_PreservesNameAndValue()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.2b — Properties also round-trip after Stage 4 lands (cross-stage check).
    [Test] public async Task Decompress_AfterCompress_PreservesProperties()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.3 — Compressed bytes, when gunzipped, deserialize to a valid application/plang
    //        document with a populated signature field (sign-if-missing fires during
    //        the in-process byte conversion).
    [Test] public async Task CompressedBytes_OnceGunzipped_ParseToApplicationPlangDocWithSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.5 — Non-compressible Type returns self unchanged from Compress (no-op).
    [Test] public async Task Compress_OnNonCompressibleType_ReturnsSelfUnchanged()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.6 — Compress routes through Serializers.Get("application/plang"), not a direct
    //        JsonSerializer.SerializeToUtf8Bytes call. The direct call in
    //        data/this.Envelope.cs is gone.
    [Test] public async Task Compress_RoutesThrough_RegisteredApplicationPlangSerializer()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.7 — _envelopeJsonOptions field is deleted (no duplicate STJ options block).
    [Test] public async Task DataTransport_EnvelopeJsonOptionsField_NoLongerExists()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 3.8 — RehydrateNestedData is gone — the wire converter handles nested-Data natively.
    [Test] public async Task DataTransport_RehydrateNestedData_MethodNoLongerExists()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
