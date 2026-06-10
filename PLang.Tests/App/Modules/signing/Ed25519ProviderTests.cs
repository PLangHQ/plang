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

    #region Sign

    [Test]
    public async Task Sign_ProducesNonEmpty64ByteSignature()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var data = Encoding.UTF8.GetBytes("hello");

        var result = _provider.Sign(data, kp.PrivateKey);
        await result.IsSuccess();
        var signature = ((global::app.type.binary.@this)(await result.Value())!).Value;

        await Assert.That(signature.Length).IsEqualTo(64);
        await Assert.That(signature.Any(b => b != 0)).IsTrue();
    }

    [Test]
    public async Task Sign_DifferentData_DifferentSignatures()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var result1 = _provider.Sign(Encoding.UTF8.GetBytes("hello"), kp.PrivateKey);
        var result2 = _provider.Sign(Encoding.UTF8.GetBytes("world"), kp.PrivateKey);
        var sig1 = ((global::app.type.binary.@this)(await result1.Value())!).Value;
        var sig2 = ((global::app.type.binary.@this)(await result2.Value())!).Value;

        await Assert.That(sig1.SequenceEqual(sig2)).IsFalse();
    }

    [Test]
    public async Task Sign_InvalidBase64PrivateKey_ReturnsSigningError()
    {
        var data = Encoding.UTF8.GetBytes("hello");
        var result = _provider.Sign(data, "not-valid-base64!!!");

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SigningError");
    }

    #endregion

    #region Verify

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var data = Encoding.UTF8.GetBytes("test data");
        var signature = ((global::app.type.binary.@this)(await _provider.Sign(data, kp.PrivateKey).Value())!).Value;

        var result = _provider.Verify(data, signature, kp.PublicKey);

        await result.IsSuccess();
        await Assert.That((bool)(await result.Value())!).IsTrue();
    }

    [Test]
    public async Task Verify_WrongData_ReturnsError()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var signature = ((global::app.type.binary.@this)(await _provider.Sign(Encoding.UTF8.GetBytes("hello"), kp.PrivateKey).Value())!).Value;

        var result = _provider.Verify(Encoding.UTF8.GetBytes("different"), signature, kp.PublicKey);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task Verify_WrongPublicKey_ReturnsError()
    {
        var kp1 = _provider.GenerateKeyPair().keys!;
        var kp2 = _provider.GenerateKeyPair().keys!;
        var data = Encoding.UTF8.GetBytes("test data");
        var signature = ((global::app.type.binary.@this)(await _provider.Sign(data, kp1.PrivateKey).Value())!).Value;

        var result = _provider.Verify(data, signature, kp2.PublicKey);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task Verify_TamperedSignature_ReturnsError()
    {
        var kp = _provider.GenerateKeyPair().keys!;
        var data = Encoding.UTF8.GetBytes("test data");
        var signature = ((global::app.type.binary.@this)(await _provider.Sign(data, kp.PrivateKey).Value())!).Value;

        // Flip the first byte
        signature[0] = (byte)(signature[0] ^ 0xFF);

        var result = _provider.Verify(data, signature, kp.PublicKey);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task Verify_InvalidBase64PublicKey_ReturnsSignatureInvalid()
    {
        var data = Encoding.UTF8.GetBytes("hello");
        var signature = new byte[64];

        var result = _provider.Verify(data, signature, "not-valid-base64!!!");

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    #endregion

    #region Config Defaults

    [Test]
    public async Task Config_DefaultProvider_IsEd25519()
    {
        var config = new global::app.module.signing.Config();
        await Assert.That(config.Provider).IsEqualTo("ed25519");
    }

    [Test]
    public async Task Config_DefaultTimeoutMs_Is300000()
    {
        var config = new global::app.module.signing.Config();
        await Assert.That(config.TimeoutMs).IsEqualTo(300_000L);
    }

    #endregion
}
