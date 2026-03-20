using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests the verify action handler. All 9 error keys covered.
/// Uses PLangEngine. Helper signs data first for verification tests.
///
/// Verify checks in order: Type → Algorithm → Nonce → Created → Expires → Contracts → Headers → HashedData → Signature
/// Each check has a specific error key for early rejection.
/// </summary>
public class VerifyActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_verify_" + Guid.NewGuid().ToString("N")[..8]);
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

    // Helper: Sign data and return the SignedData object.
    // Implementation will create identity + sign handler inline.
    // private async Task<SignedData> SignHelper(object data, List<string>? contracts = null,
    //     int? expiresInSeconds = null, Dictionary<string, object>? headers = null) { ... }

    #region Happy Path

    [Test]
    public async Task Verify_ValidSignature_SetsVerifiedOk()
    {
        // Verified.Success == true.
        //
        // Arrange: create identity, sign data
        // Act: verify with matching contracts
        // Assert: result.Success == true, signedData.Verified.Success == true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Error Keys (9 specific errors)

    [Test]
    public async Task Verify_NoSignature_Error()
    {
        // Data without signature → "NoSignature".
        //
        // Arrange: create Data with no SignedData attached
        // Act: verify
        // Assert: result.Error.Key == "NoSignature"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_UnknownAlgorithm_Error()
    {
        // Unknown algorithm → "ProviderNotFound".
        //
        // Arrange: sign data, tamper Algorithm field to "unknown-algo"
        // Act: verify
        // Assert: result.Error.Key == "ProviderNotFound"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_Expired_Error()
    {
        // Expires in past → "Expired".
        //
        // Arrange: sign with ExpiresInSeconds=1, wait >1 second
        // Act: verify
        // Assert: result.Error.Key == "Expired"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_TimedOut_Error()
    {
        // Created older than TimeoutSeconds → "TimedOut".
        //
        // Arrange: sign data, tamper Created to distant past
        // Act: verify with TimeoutSeconds that the created time exceeds
        // Assert: result.Error.Key == "TimedOut"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_NonceReplay_Error()
    {
        // Same nonce twice → "NonceReplay".
        //
        // Arrange: sign data, verify once (caches nonce)
        // Act: verify same SignedData again (same nonce)
        // Assert: second result.Error.Key == "NonceReplay"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_ContractMismatch_Error()
    {
        // Required ["C1"], signed ["C0"] → "ContractMismatch".
        //
        // Arrange: sign with contracts ["C0"]
        // Act: verify requiring contracts ["C1"]
        // Assert: result.Error.Key == "ContractMismatch"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_HeaderMismatch_Error()
    {
        // Signed headers don't match expected → "HeaderMismatch".
        //
        // Arrange: sign with headers { "method": "POST" }
        // Act: verify expecting headers { "method": "GET" }
        // Assert: result.Error.Key == "HeaderMismatch"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_DataHashMismatch_Error()
    {
        // Tampered data → "DataHashMismatch".
        //
        // Arrange: sign data { "amount": 100 }
        // Act: tamper data to { "amount": 999 }, verify
        // Assert: result.Error.Key == "DataHashMismatch"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_SignatureInvalid_Error()
    {
        // Tampered signature bytes → "SignatureInvalid".
        //
        // Arrange: sign data, flip bits in Signature field
        // Act: verify
        // Assert: result.Error.Key == "SignatureInvalid"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Contract Matching

    [Test]
    public async Task Verify_ContractMatch_OrderIndependent()
    {
        // ["C1","C0"] vs ["C0","C1"] → OK (set equality).
        //
        // Arrange: sign with contracts ["C0", "C1"]
        // Act: verify requiring ["C1", "C0"]
        // Assert: result.Success == true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_ContractsRequired_NullReturnsError()
    {
        // Contracts always required on verify — null contracts → error.
        //
        // Arrange: sign data
        // Act: verify with null contracts
        // Assert: result.Success == false (contracts are required)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_WithContracts_HappyPath()
    {
        // Sign with ["C0","C1"], verify requiring same → OK.
        //
        // Arrange: sign with contracts ["C0", "C1"]
        // Act: verify with contracts ["C0", "C1"]
        // Assert: result.Success == true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Provider Resolution

    [Test]
    public async Task Verify_ResolvesProviderFromAlgorithm_NotSettings()
    {
        // Settings point to mock, but verify uses Algorithm from SignedData.
        //
        // Arrange: register "mock" as default signing provider in settings
        //          sign data (will use Algorithm="ed25519" from the actual signing)
        // Act: verify — should resolve "ed25519" provider from Algorithm field, not "mock" from settings
        // Assert: verification succeeds (ed25519 provider used, not mock)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Headers

    [Test]
    public async Task Verify_NoExpectedHeaders_SkipsCheck()
    {
        // Null expected headers, signed has headers → OK (no check).
        //
        // Arrange: sign with headers { "method": "POST" }
        // Act: verify with null expected headers
        // Assert: result.Success == true (header check skipped)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Nonce Cache Contract

    [Test]
    public async Task Verify_FreshNonce_StoredInCache()
    {
        // After verify, nonce is in cache (write side of nonce contract).
        //
        // Arrange: sign data
        // Act: verify (first time, succeeds)
        // Assert: engine.Cache.GetAsync(nonce) returns non-null
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Boundary Conditions

    [Test]
    public async Task Verify_CreatedJustWithinTimeout_Succeeds()
    {
        // Boundary: TimeoutSeconds - 1s → OK.
        //
        // Arrange: sign data, Created is just within the timeout window
        // Act: verify with TimeoutSeconds that barely accepts it
        // Assert: result.Success == true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Verify_ProviderThrows_ReturnsDataFromError()
    {
        // Mock throws → Data.FromError().
        //
        // Arrange: register ThrowingMockProvider for "ed25519"
        //          sign data (will need to manually construct SignedData since provider throws)
        // Act: verify
        // Assert: result.Success == false, result.Error.Exception is not null
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion
}
