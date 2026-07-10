using System.Text;
using app.variable;
using app.module.code;
using app.module.signing.code;
using app.module.signing;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Direct Ed25519 tests — no engine needed.
/// Tests key generation, signing, and verification at the provider level.
/// </summary>
public class Ed25519ProviderTests
{
    private readonly Ed25519 _provider = new();

    #region Identity & Interfaces

    [Test]
    public async Task Name_ReturnsEd25519()
    {
        await Assert.That(_provider.Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task ImplementsISigningProviderAndIKeyProvider()
    {
        await Assert.That(_provider is ISigning).IsTrue();
        await Assert.That(_provider is IKey).IsTrue();
    }

    #endregion

    #region Key Generation

    [Test]
    public async Task GenerateKeyPair_ReturnsBase64Keys()
    {
        var kp = _provider.GenerateKeyPair().keys!;

        // Should not throw — valid base64
        var pubBytes = Convert.FromBase64String(kp.PublicKey);
        var privBytes = Convert.FromBase64String(kp.PrivateKey);

        await Assert.That(pubBytes.Length).IsGreaterThan(0);
        await Assert.That(privBytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateKeyPair_PublicKeyIs32Bytes()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var pubBytes = Convert.FromBase64String(kp.PublicKey);

        await Assert.That(pubBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task GenerateKeyPair_PrivateKeyIs32Bytes()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var privBytes = Convert.FromBase64String(kp.PrivateKey);

        await Assert.That(privBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task GenerateKeyPair_NonDeterministic()
    {
        var kp1 = _provider.GenerateKeyPair().keys!;
        var kp2 = _provider.GenerateKeyPair().keys!;

        await Assert.That(kp1.PublicKey).IsNotEqualTo(kp2.PublicKey);
        await Assert.That(kp1.PrivateKey).IsNotEqualTo(kp2.PrivateKey);
    }

    #endregion

    // The low-level Sign/Verify now take the signature whole (no decomposition at the call
    // site) and speak plang types. They return the VALUE and THROW on bad crypto input — the
    // [Code] boundary (SignAsync/VerifyAsync) maps throws → SignatureInvalid/SigningError.
    private static global::app.type.item.signature.@this Unsigned(string identityPublicKey, string nonce = "nonce-1")
        => new(
            value: TestApp.SharedContext.Ok(new global::app.type.item.text.@this("payload")),
            algorithm: new global::app.type.item.text.@this("ed25519"),
            nonce: new global::app.type.item.text.@this(nonce),
            created: new global::app.type.item.datetime.@this(DateTimeOffset.UnixEpoch),
            identity: new global::app.type.item.text.@this(identityPublicKey),
            hash: new global::app.module.crypto.type.hash.@this(Array.Empty<byte>(), "keccak256"),
            signature: new global::app.type.item.binary.@this(Array.Empty<byte>()));

    // A copy of a signed signature with one field swapped — for the mismatch tests.
    private static global::app.type.item.signature.@this Rebuilt(global::app.type.item.signature.@this s,
        string? nonce = null, string? identity = null, global::app.type.item.binary.@this? signature = null)
        => new(s.Value, s.Algorithm,
            nonce == null ? s.Nonce : new global::app.type.item.text.@this(nonce), s.Created,
            identity == null ? s.Identity : new global::app.type.item.text.@this(identity),
            s.Hash, signature ?? s.Signature, s.Expires, s.Contracts);

    private global::app.type.item.signature.@this Signed(KeyPair kp, string nonce = "nonce-1")
    {
        var unsigned = Unsigned(kp.PublicKey, nonce);
        return unsigned.Signed(_provider.Sign(unsigned, new global::app.type.item.text.@this(kp.PrivateKey)));
    }

    #region Sign

    [Test]
    public async Task Sign_ProducesNonEmpty64ByteSignature()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var signature = _provider.Sign(Unsigned(kp.PublicKey), new global::app.type.item.text.@this(kp.PrivateKey)).Value;

        await Assert.That(signature.Length).IsEqualTo(64);
        await Assert.That(signature.Any(b => b != 0)).IsTrue();
    }

    [Test]
    public async Task Sign_DifferentData_DifferentSignatures()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var privateKey = new global::app.type.item.text.@this(kp.PrivateKey);
        var sig1 = _provider.Sign(Unsigned(kp.PublicKey, "nonce-1"), privateKey).Value;
        var sig2 = _provider.Sign(Unsigned(kp.PublicKey, "nonce-2"), privateKey).Value;

        await Assert.That(sig1.SequenceEqual(sig2)).IsFalse();
    }

    [Test]
    public async Task Sign_InvalidBase64PrivateKey_Throws()
    {
        await Assert.That(() => _provider.Sign(Unsigned("pub"), new global::app.type.item.text.@this("not-valid-base64!!!")))
            .Throws<FormatException>();
    }

    #endregion

    #region Verify

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        await Assert.That(_provider.Verify(Signed(kp)).Value).IsTrue();
    }

    [Test]
    public async Task Verify_WrongData_ReturnsFalse()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        // Same signature bytes but different signing payload (changed nonce) — no longer matches.
        var tampered = Rebuilt(Signed(kp), nonce: "nonce-CHANGED");
        await Assert.That(_provider.Verify(tampered).Value).IsFalse();
    }

    [Test]
    public async Task Verify_WrongPublicKey_ReturnsFalse()
    {
        var kp1 = _provider.GenerateKeyPair().keys!;
        var kp2 = _provider.GenerateKeyPair().keys!;
        var wrongIdentity = Rebuilt(Signed(kp1), identity: kp2.PublicKey);
        await Assert.That(_provider.Verify(wrongIdentity).Value).IsFalse();
    }

    [Test]
    public async Task Verify_TamperedSignature_ReturnsFalse()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var signed = Signed(kp);
        var bytes = (byte[])signed.Signature.Value.Clone();
        bytes[0] ^= 0xFF;
        var tampered = Rebuilt(signed, signature: new global::app.type.item.binary.@this(bytes));
        await Assert.That(_provider.Verify(tampered).Value).IsFalse();
    }

    [Test]
    public async Task Verify_InvalidBase64PublicKey_Throws()
    {
        var signed = Unsigned("not-valid-base64!!!").Signed(new global::app.type.item.binary.@this(new byte[64]));
        await Assert.That(() => _provider.Verify(signed)).Throws<FormatException>();
    }

    #endregion

    // (Removed Config_Default* — signing.Config dissolved. Provider selection is the [Code]
    // mechanism; the 300000ms freshness default is now verify's [Default(300_000)], resolved by
    // the setting cascade and exercised by the freshness tests above.)
}
