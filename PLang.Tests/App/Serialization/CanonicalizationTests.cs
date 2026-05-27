namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Canonicalization fix: crypto/Default.cs Hash must canonicalize through the SAME
// Transport.ForOutbound options the wire serializer uses, so hashed-bytes ≡ wire-bytes.
// Today's gap: default STJ respects [JsonIgnore] on Signature, so inner signatures
// are emitted on the wire but stripped from the hash → outer sig doesn't bind them.
// Coverage matrix rows 2.11, 2.12.

public class CanonicalizationTests
{
    // 2.11 — crypto.Hash canonicalizes through Transport.ForOutbound options.
    [Test] public async Task CryptoHash_UsesTransportForOutboundOptions_NotDefaultStj()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.11b — Hash bytes for a Data == wire-serializer bytes minus only the outermost
    //         Signature field (excluded by Signature.SigningOptions filter).
    [Test] public async Task CryptoHash_BytesMatch_WireSerializerBytesMinusOuterSignature()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.12 — Outer signature binds inner signature for structurally-nested Data.
    //         Mutating an inner signature in the wire JSON fails outer verification.
    [Test] public async Task OuterSignature_BindsInnerSignature_TamperingInnerFailsOuterVerify()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
