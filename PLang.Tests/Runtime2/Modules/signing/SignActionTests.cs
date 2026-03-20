using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests the sign action handler. Uses PLangEngine setup.
/// Identity auto-created via engine for signing operations.
/// </summary>
public class SignActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_sign_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    #region Happy Path & Field Population

    [Test]
    public async Task Sign_HappyPath_AllFieldsPopulated()
    {
        // SignedData has Type, Algorithm, Nonce, Created, Identity, HashedData, Signature all non-null.
        //
        // Arrange: create identity, create Sign handler with Data = { "message": "hello" }
        // Act: Run()
        // Assert: result.Success, all SignedData fields populated
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_Type_IsSignature_Algorithm_IsEd25519()
    {
        // Type="signature", Algorithm="ed25519".
        //
        // Arrange: create identity, sign data
        // Act: Run()
        // Assert: signedData.Type == "signature", signedData.Algorithm == "ed25519"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_Identity_MatchesPublicKey()
    {
        // SignedData.Identity == engine identity's public key.
        //
        // Arrange: create identity, capture public key
        // Act: sign data
        // Assert: signedData.Identity == publicKey
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    [Test]
    public async Task Sign_Created_IsApproximatelyNow()
    {
        // SignedData.Created should be approximately DateTimeOffset.UtcNow.
        //
        // Arrange: create identity, capture time before signing
        // Act: sign data
        // Assert: signedData.Created is within 5 seconds of captured time
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Contracts

    [Test]
    public async Task Sign_DefaultContracts_IsC0()
    {
        // No contracts → ["C0"].
        //
        // Arrange: create identity, sign without specifying contracts
        // Act: Run()
        // Assert: signedData.Contracts is ["C0"]
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_CustomContracts()
    {
        // Pass ["C0","C1"] → both present.
        //
        // Arrange: create identity, sign with Contracts = ["C0", "C1"]
        // Act: Run()
        // Assert: signedData.Contracts contains both "C0" and "C1"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region TTL / Expiry

    [Test]
    public async Task Sign_TTL_SetsExpires()
    {
        // ExpiresInMs=5000 → Expires ≈ Created + 5000ms.
        //
        // Arrange: create identity, sign with ExpiresInMs = 5000
        // Act: Run()
        // Assert: signedData.Expires is approximately signedData.Created + 5000 milliseconds
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_NoTTL_ExpiresIsNull()
    {
        // No TTL → null.
        //
        // Arrange: create identity, sign without TTL
        // Act: Run()
        // Assert: signedData.Expires is null
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Headers & HashedData

    [Test]
    public async Task Sign_Headers_IncludedWhenProvided()
    {
        // Dict passed → present in SignedData; not passed → null.
        //
        // Arrange: create identity, sign with Headers = { "method": "POST" }
        // Act: Run()
        // Assert: signedData.Headers["method"] == "POST"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_HashedData_Base64Encoding()
    {
        // HashedData.Hash is valid base64, HashedData.Type="hash".
        //
        // Arrange: create identity, sign data
        // Act: Run()
        // Assert: Convert.FromBase64String(signedData.HashedData.Hash) succeeds,
        //         signedData.HashedData.Type == "hash"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Cryptographic Validity

    [Test]
    public async Task Sign_Signature_CryptographicallyValid()
    {
        // Verify via Ed25519Provider against reconstructed JSON.
        //
        // Arrange: create identity, sign data
        // Act: reconstruct JSON bytes with Signature=null, verify with Ed25519Provider
        // Assert: Ed25519Provider.Verify returns true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Provider Override

    [Test]
    public async Task Sign_ProviderOverride_UsesNamedProvider()
    {
        // Provider="mock" → mock called.
        //
        // Arrange: register MockSigningProvider named "mock", create identity
        // Act: sign with Provider = "mock"
        // Assert: mock provider's Sign was invoked (check via marker output)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_ProviderOverride_Unknown_ReturnsError()
    {
        // Provider="unknown" → "ProviderNotFound" error.
        //
        // Arrange: create identity
        // Act: sign with Provider = "unknown"
        // Assert: result.Error.Key == "ProviderNotFound"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Empty Contracts

    [Test]
    public async Task Sign_EmptyContracts_ReturnsError()
    {
        // Empty contracts [] should be rejected — at least one contract required.
        //
        // Arrange: create identity, sign with Contracts = new List<string>() (empty)
        // Act: Run()
        // Assert: result.Success == false, error indicates contracts required
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Error Paths

    [Test]
    public async Task Sign_MissingIdentity_ReturnsError()
    {
        // No identity → error.
        //
        // Arrange: fresh engine, do NOT create identity
        //          (need to prevent auto-create for this test — implementation detail TBD)
        // Act: sign data
        // Assert: result.Success == false, error indicates missing identity
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Sign_ProviderThrows_ReturnsDataFromError()
    {
        // Mock throws → Data.FromError().
        //
        // Arrange: register ThrowingMockProvider, create identity
        // Act: sign data
        // Assert: result.Success == false, result.Error.Exception is not null
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion
}
