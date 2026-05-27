namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 3: Multi-actor forwarding chain.
//
// Setup: three actor contexts A, B, C with distinct signing identities.
// Capture:
//   1) Context A writes d1 = Data("user", {firstName: "Ingi"}). Sign-if-missing → A signs.
//   2) Context B reads bytes A as d1Received. Wraps as d3 = Data("forwarded", d1Received).
//      Writes d3. Sign-if-missing → B signs d3; the walk into d3.Value sees d1Received
//      already signed → skip.
//   3) Context C reads bytes B as d3Received.
//
// Proves:
//   - sign-if-missing is idempotent (walk doesn't re-sign already-signed Data)
//   - forwarding preserves provenance without explicit choreography
//   - canonicalization covers structurally-nested inner signatures (Stage 2 fix)

public class Cut3_MultiActorForwardingTests
{
    // Outer d3Received.Signature.Identity == B.
    [Test] public async Task Cut3_OuterData_CarriesForwardersSigningIdentity()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Inner Data (in d3Received.Value) has Signature.Identity == A. Chain preserved across wrap.
    [Test] public async Task Cut3_InnerData_RetainsOriginalSignersIdentityAfterWrap()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Both signatures verify independently against their respective bytes.
    [Test] public async Task Cut3_BothSignatures_VerifyIndependently()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Mutating ANY byte of d3.Value (incl. its `signature` sub-object) fails outer verify.
    // This is the Stage 2 canonicalization fix in action — the inner signature is now
    // part of the canonicalized bytes the outer signature binds.
    [Test] public async Task Cut3_TamperingInnerSignatureSubObject_FailsOuterVerify()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
