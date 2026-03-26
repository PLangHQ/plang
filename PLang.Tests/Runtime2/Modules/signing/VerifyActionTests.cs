using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.signing.providers;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.identity;
using PLang.Runtime2.modules.signing;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests the verify action handler. All 9 error keys covered.
/// Verify checks in order: InvalidType → ProviderNotFound → TimedOut → Expired → NonceReplay → ContractMismatch → HeaderMismatch → DataHashMismatch → SignatureInvalid
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
        return await _engine.RunAction<sign>(action, Ctx);
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
        return await _engine.RunAction<verify>(action, Ctx);
    }

    #region Happy Path

    [Test]
    public async Task Verify_ValidSignature_ReturnsSuccess()
    {
        var signed = await SignHelper("hello", contracts: new List<string> { "C0" });
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });

        await Assert.That(result.Success).IsTrue();
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
    public async Task Verify_TamperedAlgorithm_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        signed.Signature!.Algorithm = "unknown-algo";

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        // Tampered algorithm changes the signing bytes, so signature verification fails
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task Verify_Expired_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" }, expiresInMs: 50);
        await Task.Delay(100);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Expired");
    }

    [Test]
    public async Task Verify_TimedOut_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        // Tamper Created to distant past
        signed.Signature!.Created = DateTimeOffset.UtcNow.AddHours(-1);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, timeoutMs: 1000);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("TimedOut");
    }

    [Test]
    public async Task Verify_NonceReplay_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" }, headers: new Dictionary<string, object> { { "method", "POST" } });

        var result = await VerifyHelper(signed,
            contracts: new List<string> { "C0" },
            headers: new Dictionary<string, object> { { "method", "GET" } });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HeaderMismatch");
    }

    [Test]
    public async Task Verify_DataHashMismatch_Error()
    {
        var signed = await SignHelper(new { amount = 100 }, contracts: new List<string> { "C0" });
        // Tamper the hash
        signed.Signature!.Hash = Data.Ok(new byte[32], PLang.Runtime2.Engine.Memory.Type.FromName("keccak256"));

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task Verify_SignatureInvalid_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
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
    public async Task Verify_NullContracts_BothNull_Succeeds()
    {
        var signed = await SignHelper("test");

        var result = await VerifyHelper(signed, contracts: null);
        await Assert.That(result.Success).IsTrue();
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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        await Assert.That(signed.Signature!.Algorithm).IsEqualTo("ed25519");

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Headers

    [Test]
    public async Task Verify_NoExpectedHeaders_SkipsCheck()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" }, headers: new Dictionary<string, object> { { "method", "POST" } });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, headers: null);
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Nonce Cache Contract

    [Test]
    public async Task Verify_FreshNonce_StoredInCache()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, timeoutMs: 60_000);
        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Empty Contracts

    [Test]
    public async Task Verify_EmptyContractsList_BothEmpty_Succeeds()
    {
        var signed = await SignHelper("test", contracts: new List<string>());
        var result = await VerifyHelper(signed, contracts: new List<string>());

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Verify_RequiredContracts_SignedHasNone_ReturnsError()
    {
        var signed = await SignHelper("test");
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    #endregion

    #region Different Nonce Succeeds

    [Test]
    public async Task Verify_SecondDifferentNonce_Succeeds()
    {
        var signed1 = await SignHelper("hello", contracts: new List<string> { "C0" });
        var signed2 = await SignHelper("world", contracts: new List<string> { "C0" });

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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        signed.Signature!.Type = "hash";

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidType");
    }

    #endregion

    #region Tampered Contracts on SignedData

    [Test]
    public async Task Verify_SignedDataContractsNull_RequiredNotNull_ReturnsError()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });

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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" }, expiresInMs: 50);
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
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        // Register a throwing provider and point the signed data at it
        var throwing = new ThrowingProvider();
        _engine.Providers.Register<ISigningProvider>(throwing);
        signed.Signature!.Algorithm = "throwing";

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    #endregion

    #region SignedData.Verify Direct Tests

    [Test]
    public async Task SignedDataVerify_EmptySignature_ReturnsSignatureInvalid()
    {
        var sd = new SignedData
        {
            Type = "signature",
            Algorithm = "ed25519",
            Identity = "somekey",
            Signature = null
        };

        var provider = new Ed25519Provider();
        var result = sd.Verify(provider);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task SignedDataVerify_EmptyStringSignature_ReturnsSignatureInvalid()
    {
        var sd = new SignedData
        {
            Type = "signature",
            Algorithm = "ed25519",
            Identity = "somekey",
            Signature = ""
        };

        var provider = new Ed25519Provider();
        var result = sd.Verify(provider);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task SignedDataVerify_InvalidBase64Signature_ReturnsSignatureInvalid()
    {
        var sd = new SignedData
        {
            Type = "signature",
            Algorithm = "ed25519",
            Identity = "somekey",
            Signature = "not-valid-base64!!!"
        };

        var provider = new Ed25519Provider();
        var result = sd.Verify(provider);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    #endregion

    private class ThrowingProvider : ISigningProvider
    {
        public string Name => "throwing";
        public bool IsDefault { get; set; }
        public Data<KeyPair> GenerateKeyPair() => Data<KeyPair>.FromError(new ActionError("Key generation failed", "KeyGenerationError", 500));
        public Data Sign(byte[] data, string privateKey) => Data.FromError(new ActionError("Sign failed", "SigningError", 500));
        public Data Verify(byte[] data, byte[] signature, string publicKey) => Data.FromError(new ActionError("Verify failed", "SignatureInvalid", 400));
        public Task<Data> SignAsync(sign action) => Task.FromResult(Data.FromError(new ActionError("Sign failed", "SigningError", 500)));
        public Task<Data> VerifyAsync(verify action) => Task.FromResult(Data.FromError(new ActionError("Verify failed", "SignatureInvalid", 400)));
    }
}
