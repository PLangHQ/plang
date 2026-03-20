using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests the verify action handler. All 9 error keys covered.
/// Uses PLangEngine. Helper signs data first for verification tests.
///
/// Verify checks in order: NoSignature → ProviderNotFound → TimedOut → Expired → NonceReplay → ContractMismatch → HeaderMismatch → DataHashMismatch → SignatureInvalid
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
    //     int? expiresInMs = null, Dictionary<string, object>? headers = null) { ... }

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
        // Arrange: sign with ExpiresInMs=50, wait >50ms
        // Act: verify
        // Assert: result.Error.Key == "Expired"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_TimedOut_Error()
    {
        // Created older than TimeoutMs → "TimedOut".
        //
        // Arrange: sign data, tamper Created to distant past
        // Act: verify with TimeoutMs that the created time exceeds
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
        // Contracts always required on verify — null contracts → "ContractMismatch".
        //
        // Arrange: sign data
        // Act: verify with null contracts
        // Assert: result.Success == false, result.Error.Key == "ContractMismatch"
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
        // Boundary: TimeoutMs minus a small margin → OK.
        //
        // Arrange: sign data, Created is just within the timeout window
        // Act: verify with TimeoutMs that barely accepts it
        // Assert: result.Success == true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Empty Contracts

    [Test]
    public async Task Verify_EmptyContractsList_ReturnsError()
    {
        // Empty list [] is invalid — contracts are required on verify.
        //
        // Arrange: sign data with default contracts
        // Act: verify with Contracts = new List<string>() (empty)
        // Assert: result.Success == false, result.Error.Key == "ContractMismatch"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Lazy Verification (DESIGN-1)

    [Test]
    public async Task Verify_LazyAccess_TriggersVerificationWithoutExplicitStep()
    {
        // Accessing SignedData.Verified without calling verify action must auto-verify.
        // Lazy path uses default contracts ['C0'] and no headers.
        //
        // Arrange: create identity, sign data (default contracts ['C0'])
        // Act: access signedData.Verified directly — do NOT call verify action
        // Assert: Verified is not null, Verified.Success == true
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_LazyAccess_CachesResult_SecondAccessSameObject()
    {
        // First access triggers verification, second access returns cached result.
        //
        // Arrange: create identity, sign data
        // Act: access signedData.Verified twice
        // Assert: both return same result, verification logic only ran once
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_LazyAccess_UsesDefaultContractsC0()
    {
        // Lazy path uses ['C0']. If signed with ['C1'], lazy verify fails with ContractMismatch.
        //
        // Arrange: create identity, sign data with contracts ['C1']
        // Act: access signedData.Verified directly (lazy path uses ['C0'])
        // Assert: Verified.Success == false, error key == "ContractMismatch"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_ExplicitThenLazy_ExplicitResultPreserved()
    {
        // Explicit verify sets Verified. Subsequent lazy access returns cached result, not re-verify.
        //
        // Arrange: create identity, sign data with ['C0','C1']
        // Act: explicitly verify with contracts ['C0','C1'] (succeeds), then access .Verified again
        // Assert: Verified.Success == true (explicit result preserved, not overwritten by lazy)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Different Nonce Succeeds

    [Test]
    public async Task Verify_SecondDifferentNonce_Succeeds()
    {
        // Two different signed messages (different nonces) both verify — no false positive.
        //
        // Arrange: create identity, sign two different Data objects (each gets unique nonce)
        // Act: verify first, then verify second
        // Assert: both succeed (different nonces don't interfere)
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Tampered Type Field

    [Test]
    public async Task Verify_TamperedType_ReturnsError()
    {
        // SignedData.Type tampered from "signature" to something else → early rejection.
        // Type is checked first in the verify chain per architect spec.
        //
        // Arrange: create identity, sign data, then tamper signedData.Type = "hash"
        // Act: verify
        // Assert: result.Success == false, error indicates invalid type
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Tampered Contracts on SignedData

    [Test]
    public async Task Verify_SignedDataContractsNull_ReturnsError()
    {
        // SignedData with null Contracts (tampered) → error during verify.
        //
        // Arrange: sign data, then tamper SignedData.Contracts = null
        // Act: verify with contracts ["C0"]
        // Assert: result.Success == false, error indicates missing contracts on signed data
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Header Direction Mismatch

    [Test]
    public async Task Verify_NoSignedHeaders_ExpectsHeaders_ReturnsHeaderMismatch()
    {
        // Signed without headers, verify expects headers → "HeaderMismatch".
        //
        // Arrange: sign data WITHOUT headers (Headers = null on SignedData)
        // Act: verify with expected headers { "method": "GET" }
        // Assert: result.Error.Key == "HeaderMismatch"
        await Assert.Fail("stub — implementation depends on signing module");
    }

    #endregion

    #region Verify Check Order

    [Test]
    public async Task Verify_ExpiredAndNonceReplay_ReturnsExpiredNotNonceReplay()
    {
        // When data has BOTH expired timestamp AND replayed nonce, error should be "Expired"
        // because Expires is checked before NonceReplay in the verify chain.
        //
        // Arrange: create identity, sign with short TTL (50ms), verify once (caches nonce),
        //          wait for expiry
        // Act: verify same data again (both expired AND nonce replayed)
        // Assert: result.Error.Key == "Expired" (not "NonceReplay")
        await Assert.Fail("stub — implementation depends on signing module");
    }

    [Test]
    public async Task Verify_TimedOutAndContractMismatch_ReturnsTimedOutNotContractMismatch()
    {
        // When data is both timed out AND has wrong contracts, error should be "TimedOut"
        // because Created/TimeoutMs is checked before contracts in the verify chain.
        //
        // Arrange: create identity, sign with contracts ["C0"], tamper Created to distant past
        // Act: verify with contracts ["C1"] and a short TimeoutMs
        // Assert: result.Error.Key == "TimedOut" (not "ContractMismatch")
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
