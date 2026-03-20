namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Direct Ed25519Provider tests — no engine needed.
/// Tests key generation, signing, and verification at the provider level.
/// </summary>
public class Ed25519ProviderTests
{
    // Provider will be: new Ed25519Provider()
    // Interface: ISigningProvider { string Name; (string,string) GenerateKeyPair(); byte[] Sign(byte[],string); bool Verify(byte[],byte[],string); }

    #region Identity & Interfaces

    [Test]
    public async Task Name_ReturnsEd25519()
    {
        // Provider.Name should be "ed25519".
        //
        // Arrange: new Ed25519Provider()
        // Assert: provider.Name == "ed25519"
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task ImplementsISigningProviderAndIKeyProvider()
    {
        // Ed25519Provider implements both ISigningProvider and IKeyProvider.
        //
        // Arrange: new Ed25519Provider()
        // Assert: provider is ISigningProvider, provider is IKeyProvider
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    #endregion

    #region Key Generation

    [Test]
    public async Task GenerateKeyPair_ReturnsBase64Keys()
    {
        // Both public and private keys should be valid base64 strings.
        //
        // Arrange: new Ed25519Provider()
        // Act: GenerateKeyPair()
        // Assert: Convert.FromBase64String succeeds for both keys
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task GenerateKeyPair_PublicKeyIs32Bytes()
    {
        // Ed25519 public key decoded = 32 bytes.
        //
        // Arrange: new Ed25519Provider()
        // Act: GenerateKeyPair()
        // Assert: Convert.FromBase64String(publicKey).Length == 32
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task GenerateKeyPair_PrivateKeyIs32Bytes()
    {
        // Ed25519 private key decoded = 32 bytes.
        //
        // Arrange: new Ed25519Provider()
        // Act: GenerateKeyPair()
        // Assert: Convert.FromBase64String(privateKey).Length == 32
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task GenerateKeyPair_NonDeterministic()
    {
        // Two calls → different keys.
        //
        // Arrange: new Ed25519Provider()
        // Act: GenerateKeyPair() twice
        // Assert: publicKey1 != publicKey2, privateKey1 != privateKey2
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    #endregion

    #region Sign

    [Test]
    public async Task Sign_ProducesNonEmpty64ByteSignature()
    {
        // Ed25519 signatures are 64 bytes.
        //
        // Arrange: GenerateKeyPair, data = UTF8 bytes of "hello"
        // Act: Sign(data, privateKey)
        // Assert: signature.Length == 64, not all zeros
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task Sign_DifferentData_DifferentSignatures()
    {
        // Distinct payloads → distinct signatures.
        //
        // Arrange: GenerateKeyPair, data1 = "hello", data2 = "world"
        // Act: Sign each
        // Assert: sig1 != sig2
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    #endregion

    #region Verify

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        // Sign then verify same data → true.
        //
        // Arrange: GenerateKeyPair, data = "test data"
        // Act: Sign(data, privateKey), Verify(data, signature, publicKey)
        // Assert: result == true
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task Verify_WrongData_ReturnsFalse()
    {
        // Sign A, verify B → false.
        //
        // Arrange: GenerateKeyPair, sign "hello"
        // Act: Verify("different", signature, publicKey)
        // Assert: result == false
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task Verify_WrongPublicKey_ReturnsFalse()
    {
        // Sign with key A, verify with key B → false.
        //
        // Arrange: GenerateKeyPair twice, sign with first private key
        // Act: Verify(data, signature, secondPublicKey)
        // Assert: result == false
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    [Test]
    public async Task Verify_TamperedSignature_ReturnsFalse()
    {
        // Flip bits in signature → false.
        //
        // Arrange: GenerateKeyPair, sign data
        // Act: flip first byte of signature, Verify
        // Assert: result == false
        await Assert.Fail("stub — implementation depends on Ed25519Provider");
    }

    #endregion
}
