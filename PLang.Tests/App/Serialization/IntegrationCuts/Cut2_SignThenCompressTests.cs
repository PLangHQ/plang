namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 2: Sign-then-compress preserves inner attestation.
//
// Setup: single actor A with a signing identity, application/plang memory channel.
// Capture: d1 = Data("user", {firstName="Ingi"}); d2 = d1.Compress();
//          await channel.WriteAsync(d2);
//
// Proves:
//   - Compress routes through the registered application/plang serializer (Stage 3)
//   - sign-if-missing fires during in-process byte conversion
//   - the sign-then-compress chain is cryptographically tight (outer signature binds
//     inner through the byte-leaf)

public class Cut2_SignThenCompressTests
{
    // Outer wire JSON: type="archived", value is base64-encoded byte[], signature populated.
    [Test] public async Task Cut2_OuterWireJson_HasArchivedTypeBytesValueAndSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Inner bytes (base64 + gunzip) decode to a serialized Data with its OWN populated
    // signature — sign-if-missing fired before the bytes were gzipped.
    [Test] public async Task Cut2_InnerBytes_DecodeToSignedInnerDataWithOwnSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // d2.Decompress() returns a Data equal to d1 in name/value, with inner signature preserved.
    [Test] public async Task Cut2_Decompress_ReturnsOriginalWithInnerSignaturePreserved()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Mutating any byte inside the outer wire `value` (post-encode) fails outer verify.
    [Test] public async Task Cut2_TamperingValueByte_FailsOuterSignatureVerify()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
