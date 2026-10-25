using NSec.Cryptography;

namespace PLang.Tests.App.Modules.signing;

/// <summary>Ed25519 verification as the library does it stays strict: a known vector verifies, and a tampered message, a
/// non-canonical S, a small-order key and a short signature are all refused (security, packages).</summary>
public class Ed25519VectorTests
{
    private static byte[] H(string hex) => System.Convert.FromHexString(hex);

    // RFC 8032 test 1 (empty message)
    private const string Pk = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";
    private const string Sig = "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private static bool Verifies(byte[] pk, byte[] message, byte[] sig)
    {
        var key = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, pk, KeyBlobFormat.RawPublicKey);
        return SignatureAlgorithm.Ed25519.Verify(key, message, sig);
    }

    [Test] public async Task TheRfcVector_Verifies() => await Assert.That(Verifies(H(Pk), [], H(Sig))).IsTrue();

    [Test] public async Task ATamperedMessage_DoesNotVerify() => await Assert.That(Verifies(H(Pk), [0], H(Sig))).IsFalse();

    [Test]
    public async Task ANonCanonicalS_DoesNotVerify()
    {
        // S + L (the group order) is the same scalar mod L: a malleable twin of the signature that must be refused
        var sig = H(Sig);
        var l = H("edd3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010");
        var carry = 0;
        for (var i = 0; i < 32; i++) { var v = sig[32 + i] + l[i] + carry; sig[32 + i] = (byte)v; carry = v >> 8; }
        await Assert.That(Verifies(H(Pk), [], sig)).IsFalse();
    }

    [Test]
    public async Task ASmallOrderKey_WithAZeroSignature_DoesNotVerify()
    {
        var identity = new byte[32]; identity[0] = 1;       // the curve's identity point
        var sig = new byte[64]; sig[0] = 1;                  // R = identity, S = 0
        bool ok;
        try { ok = Verifies(identity, [], sig); } catch (System.Exception) { ok = false; }
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task AShortSignature_DoesNotVerifyAndDoesNotThrow()
        => await Assert.That(Verifies(H(Pk), [], H(Sig)[..63])).IsFalse();
}
