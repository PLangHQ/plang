using System.Text;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Direct Ed25519Provider tests — no engine needed.
/// Tests key generation, signing, and verification at the provider level.
/// </summary>
public class Ed25519ProviderTests
{
    private readonly Ed25519Provider _provider = new();

    #region Identity & Interfaces

    [Test]
    public async Task Name_ReturnsEd25519()
    {
        await Assert.That(_provider.Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task ImplementsISigningProviderAndIKeyProvider()
    {
        await Assert.That(_provider is ISigningProvider).IsTrue();
        await Assert.That(_provider is IKeyProvider).IsTrue();
    }

    #endregion

    #region Key Generation

    [Test]
    public async Task GenerateKeyPair_ReturnsBase64Keys()
    {
        var kp = _provider.GenerateKeyPair();

        // Should not throw — valid base64
        var pubBytes = Convert.FromBase64String(kp.PublicKey);
        var privBytes = Convert.FromBase64String(kp.PrivateKey);

        await Assert.That(pubBytes.Length).IsGreaterThan(0);
        await Assert.That(privBytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateKeyPair_PublicKeyIs32Bytes()
    {
        var kp = _provider.GenerateKeyPair();
        var pubBytes = Convert.FromBase64String(kp.PublicKey);

        await Assert.That(pubBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task GenerateKeyPair_PrivateKeyIs32Bytes()
    {
        var kp = _provider.GenerateKeyPair();
        var privBytes = Convert.FromBase64String(kp.PrivateKey);

        await Assert.That(privBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task GenerateKeyPair_NonDeterministic()
    {
        var kp1 = _provider.GenerateKeyPair();
        var kp2 = _provider.GenerateKeyPair();

        await Assert.That(kp1.PublicKey).IsNotEqualTo(kp2.PublicKey);
        await Assert.That(kp1.PrivateKey).IsNotEqualTo(kp2.PrivateKey);
    }

    #endregion

    #region Sign

    [Test]
    public async Task Sign_ProducesNonEmpty64ByteSignature()
    {
        var kp = _provider.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("hello");

        var signature = _provider.Sign(data, kp.PrivateKey);

        await Assert.That(signature.Length).IsEqualTo(64);
        await Assert.That(signature.Any(b => b != 0)).IsTrue();
    }

    [Test]
    public async Task Sign_DifferentData_DifferentSignatures()
    {
        var kp = _provider.GenerateKeyPair();
        var sig1 = _provider.Sign(Encoding.UTF8.GetBytes("hello"), kp.PrivateKey);
        var sig2 = _provider.Sign(Encoding.UTF8.GetBytes("world"), kp.PrivateKey);

        await Assert.That(sig1.SequenceEqual(sig2)).IsFalse();
    }

    #endregion

    #region Verify

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        var kp = _provider.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("test data");
        var signature = _provider.Sign(data, kp.PrivateKey);

        var result = _provider.Verify(data, signature, kp.PublicKey);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Verify_WrongData_ReturnsFalse()
    {
        var kp = _provider.GenerateKeyPair();
        var signature = _provider.Sign(Encoding.UTF8.GetBytes("hello"), kp.PrivateKey);

        var result = _provider.Verify(Encoding.UTF8.GetBytes("different"), signature, kp.PublicKey);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Verify_WrongPublicKey_ReturnsFalse()
    {
        var kp1 = _provider.GenerateKeyPair();
        var kp2 = _provider.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("test data");
        var signature = _provider.Sign(data, kp1.PrivateKey);

        var result = _provider.Verify(data, signature, kp2.PublicKey);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Verify_TamperedSignature_ReturnsFalse()
    {
        var kp = _provider.GenerateKeyPair();
        var data = Encoding.UTF8.GetBytes("test data");
        var signature = _provider.Sign(data, kp.PrivateKey);

        // Flip the first byte
        signature[0] = (byte)(signature[0] ^ 0xFF);

        var result = _provider.Verify(data, signature, kp.PublicKey);

        await Assert.That(result).IsFalse();
    }

    #endregion
}
