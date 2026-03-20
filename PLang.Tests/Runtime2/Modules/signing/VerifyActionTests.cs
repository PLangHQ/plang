using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.identity;
using PLang.Runtime2.modules.signing;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests the verify action handler. All 9 error keys covered.
/// Verify checks in order: NoSignature → ProviderNotFound → TimedOut → Expired → NonceReplay → ContractMismatch → HeaderMismatch → DataHashMismatch → SignatureInvalid
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

    private async Task<Data> SignHelper(object data, List<string>? contracts = null,
        int? expiresInMs = null, Dictionary<string, object>? headers = null)
    {
        var action = new sign
        {
            Context = Ctx,
            Data = data,
            Contracts = contracts,
            ExpiresInMs = expiresInMs,
            Headers = headers
        };
        return await action.Run();
    }

    private async Task<Data> VerifyHelper(Data signedData, List<string>? contracts = null,
        Dictionary<string, object>? headers = null, long? timeoutMs = null)
    {
        var action = new verify
        {
            Context = Ctx,
            Data = signedData,
            Contracts = contracts,
            Headers = headers,
            TimeoutMs = timeoutMs
        };
        return await action.Run();
    }

    #region Happy Path

    [Test]
    public async Task Verify_ValidSignature_SetsVerifiedOk()
    {
        var signed = await SignHelper("hello");
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });

        await Assert.That(result.Success).IsTrue();
        // Verified is cached after explicit verify
        await Assert.That(signed.Signature!.Verified).IsNotNull();
        await Assert.That(signed.Signature.Verified!.Success).IsTrue();
    }

    #endregion

    #region Error Keys (9 specific errors)

    [Test]
    public async Task Verify_NoSignature_Error()
    {
        var data = Data.Ok("unsigned");
        var result = await VerifyHelper(data, contracts: new List<string> { "C0" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoSignature");
    }

    [Test]
    public async Task Verify_UnknownAlgorithm_Error()
    {
        var signed = await SignHelper("test");
        signed.Signature!.Algorithm = "unknown-algo";

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Verify_Expired_Error()
    {
        var signed = await SignHelper("test", expiresInMs: 50);
        await Task.Delay(100);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Expired");
    }

    [Test]
    public async Task Verify_TimedOut_Error()
    {
        var signed = await SignHelper("test");
        // Tamper Created to distant past
        signed.Signature!.Created = DateTimeOffset.UtcNow.AddHours(-1);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, timeoutMs: 1000);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("TimedOut");
    }

    [Test]
    public async Task Verify_NonceReplay_Error()
    {
        var signed = await SignHelper("test");
        // First verify — succeeds and caches nonce
        var first = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(first.Success).IsTrue();

        // Second verify — same nonce → replay
        var second = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(second.Success).IsFalse();
        await Assert.That(second.Error!.Key).IsEqualTo("NonceReplay");
    }

    [Test]
    public async Task Verify_ContractMismatch_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C1" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    [Test]
    public async Task Verify_HeaderMismatch_Error()
    {
        var signed = await SignHelper("test", headers: new Dictionary<string, object> { { "method", "POST" } });

        var result = await VerifyHelper(signed,
            contracts: new List<string> { "C0" },
            headers: new Dictionary<string, object> { { "method", "GET" } });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HeaderMismatch");
    }

    [Test]
    public async Task Verify_DataHashMismatch_Error()
    {
        var signed = await SignHelper(new { amount = 100 });
        // Tamper the hash
        signed.Signature!.HashedData.Hash = Convert.ToBase64String(new byte[32]);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        // Re-hashing the original data detects the tampered hash
        await Assert.That(result.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task Verify_SignatureInvalid_Error()
    {
        var signed = await SignHelper("test");
        // Flip first byte of signature
        var sigBytes = Convert.FromBase64String(signed.Signature!.Signature!);
        sigBytes[0] ^= 0xFF;
        signed.Signature.Signature = Convert.ToBase64String(sigBytes);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    #endregion

    #region Contract Matching

    [Test]
    public async Task Verify_ContractMatch_OrderIndependent()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0", "C1" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C1", "C0" });
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Verify_ContractsRequired_NullReturnsError()
    {
        var signed = await SignHelper("test");

        var result = await VerifyHelper(signed, contracts: null);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    [Test]
    public async Task Verify_WithContracts_HappyPath()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0", "C1" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0", "C1" });
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Provider Resolution

    [Test]
    public async Task Verify_ResolvesProviderFromAlgorithm_NotSettings()
    {
        // Register a mock as default — but sign uses ed25519
        // Verify should use Algorithm from SignedData, not the default provider
        var signed = await SignHelper("test");
        await Assert.That(signed.Signature!.Algorithm).IsEqualTo("ed25519");

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Headers

    [Test]
    public async Task Verify_NoExpectedHeaders_SkipsCheck()
    {
        var signed = await SignHelper("test", headers: new Dictionary<string, object> { { "method", "POST" } });

        // Verify with null expected headers — should skip header check
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, headers: null);
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Nonce Cache Contract

    [Test]
    public async Task Verify_FreshNonce_StoredInCache()
    {
        var signed = await SignHelper("test");
        var nonce = signed.Signature!.Nonce;

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsTrue();

        // Check nonce is in cache
        var cached = await _engine.Cache.GetAsync($"nonce:{nonce}");
        await Assert.That(cached).IsNotNull();
    }

    #endregion

    #region Boundary Conditions

    [Test]
    public async Task Verify_CreatedJustWithinTimeout_Succeeds()
    {
        var signed = await SignHelper("test");
        // Created is now, timeout is generous — should pass
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, timeoutMs: 60_000);
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Empty Contracts

    [Test]
    public async Task Verify_EmptyContractsList_ReturnsError()
    {
        var signed = await SignHelper("test");
        var result = await VerifyHelper(signed, contracts: new List<string>());

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    #endregion

    #region Lazy Verification (DESIGN-1)

    [Test]
    public async Task Verify_LazyAccess_TriggersVerificationWithoutExplicitStep()
    {
        var signed = await SignHelper("test");

        // GetVerifiedAsync triggers verification without calling verify action
        var verified = await signed.Signature!.GetVerifiedAsync();
        await Assert.That(verified).IsNotNull();
        await Assert.That(verified!.Success).IsTrue();
    }

    [Test]
    public async Task Verify_LazyAccess_CachesResult_SecondAccessSameObject()
    {
        var signed = await SignHelper("test");

        var first = await signed.Signature!.GetVerifiedAsync();
        var second = await signed.Signature.GetVerifiedAsync();

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        // Same object reference — cached
        await Assert.That(first).IsSameReferenceAs(second);
    }

    [Test]
    public async Task Verify_LazyAccess_UsesDefaultContractsC0()
    {
        // Sign with C1, lazy verify uses C0 → mismatch
        var signed = await SignHelper("test", contracts: new List<string> { "C1" });

        var verified = await signed.Signature!.GetVerifiedAsync();
        await Assert.That(verified).IsNotNull();
        await Assert.That(verified!.Success).IsFalse();
        await Assert.That(verified.Error!.Key).IsEqualTo("ContractMismatch");
    }

    [Test]
    public async Task Verify_ExplicitThenLazy_ExplicitResultPreserved()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0", "C1" });

        // Explicit verify with matching contracts
        var explicit_ = await VerifyHelper(signed, contracts: new List<string> { "C0", "C1" });
        await Assert.That(explicit_.Success).IsTrue();

        // Cached result from explicit verify — Verified returns it synchronously
        var cached = signed.Signature!.Verified;
        await Assert.That(cached).IsNotNull();
        await Assert.That(cached!.Success).IsTrue();
    }

    #endregion

    #region Different Nonce Succeeds

    [Test]
    public async Task Verify_SecondDifferentNonce_Succeeds()
    {
        var signed1 = await SignHelper("hello");
        var signed2 = await SignHelper("world");

        var result1 = await VerifyHelper(signed1, contracts: new List<string> { "C0" });
        var result2 = await VerifyHelper(signed2, contracts: new List<string> { "C0" });

        await Assert.That(result1.Success).IsTrue();
        await Assert.That(result2.Success).IsTrue();
    }

    #endregion

    #region Tampered Type Field

    [Test]
    public async Task Verify_TamperedType_ReturnsError()
    {
        var signed = await SignHelper("test");
        signed.Signature!.Type = "hash";

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidType");
    }

    #endregion

    #region Tampered Contracts on SignedData

    [Test]
    public async Task Verify_SignedDataContractsNull_ReturnsError()
    {
        var signed = await SignHelper("test");
        signed.Signature!.Contracts = null;

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    #endregion

    #region Header Direction Mismatch

    [Test]
    public async Task Verify_NoSignedHeaders_ExpectsHeaders_ReturnsHeaderMismatch()
    {
        var signed = await SignHelper("test");
        // No headers on signed data

        var result = await VerifyHelper(signed,
            contracts: new List<string> { "C0" },
            headers: new Dictionary<string, object> { { "method", "GET" } });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HeaderMismatch");
    }

    #endregion

    #region Verify Check Order

    [Test]
    public async Task Verify_ExpiredAndNonceReplay_ReturnsExpiredNotNonceReplay()
    {
        var signed = await SignHelper("test", expiresInMs: 50);
        // First verify — caches nonce
        var first = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        // Might succeed or already expired
        await Task.Delay(100);

        // Second verify — both expired AND nonce replayed
        var second = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(second.Success).IsFalse();
        // Expired is checked before NonceReplay
        await Assert.That(second.Error!.Key).IsEqualTo("Expired");
    }

    [Test]
    public async Task Verify_TimedOutAndContractMismatch_ReturnsTimedOutNotContractMismatch()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        // Tamper Created to distant past
        signed.Signature!.Created = DateTimeOffset.UtcNow.AddHours(-1);

        var result = await VerifyHelper(signed,
            contracts: new List<string> { "C1" },
            timeoutMs: 1000);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("TimedOut");
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Verify_ProviderThrows_ReturnsDataFromError()
    {
        var signed = await SignHelper("test");
        // Register a throwing provider and point the signed data at it
        var throwing = new ThrowingProvider();
        _engine.Providers.Register<ISigningProvider>(throwing);
        signed.Signature!.Algorithm = "throwing";

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
    }

    #endregion

    private class ThrowingProvider : ISigningProvider
    {
        public string Name => "throwing";
        public bool IsDefault { get; set; }
        public KeyPair GenerateKeyPair() => throw new InvalidOperationException("fail");
        public byte[] Sign(byte[] data, string privateKey) => throw new InvalidOperationException("fail");
        public bool Verify(byte[] data, byte[] signature, string publicKey) => throw new InvalidOperationException("Verify failed");
    }
}
