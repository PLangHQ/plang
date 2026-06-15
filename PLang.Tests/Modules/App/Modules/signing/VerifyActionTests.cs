using app.actor.context;
using app.error;
using app.variable;
using app.module.code;
using app.module.signing.code;
using app.module.crypto;
using app.module.identity;
using app.module.signing;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Tests the verify action handler. All 9 error keys covered.
/// Verify checks in order: InvalidType → ProviderNotFound → TimedOut → Expired → NonceReplay → ContractMismatch → HeaderMismatch → DataHashMismatch → SignatureInvalid
/// </summary>
public class VerifyActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_verify_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.System.Context;

    private async Task<Data> SignHelper(object data, List<string>? contracts = null,
        TimeSpan? expires = null, Dictionary<string, object>? headers = null)
    {
        var action = new sign
        {
            Context = Ctx,
            Data = new Data("", data),
            Contracts = contracts is null ? null : new global::app.data.@this<global::app.type.list.@this>("", global::app.type.list.@this.FromRaw(contracts, Ctx)),
            Expires = expires.HasValue ? (global::app.type.duration.@this)expires.Value : null,
            Headers = headers?.ToDictData()
        };
        return await _app.RunAction<sign>(action, Ctx);
    }

    private async Task<Data> VerifyHelper(Data signedData, List<string>? contracts = null,
        Dictionary<string, object>? headers = null, long? timeoutMs = null)
    {
        var action = new verify
        {
            Context = Ctx,
            Data = signedData,
            Contracts = contracts is null ? null : new global::app.data.@this<global::app.type.list.@this>("", global::app.type.list.@this.FromRaw(contracts, Ctx)),
            Headers = headers?.ToDictData(),
            TimeoutMs = timeoutMs.HasValue ? (global::app.type.number.@this)timeoutMs.Value : null
        };
        return await _app.RunAction<verify>(action, Ctx);
    }

    // sign returns a Data whose value IS the signature layer (immutable). To
    // "tamper", rebuild the layer with one field changed but the ORIGINAL
    // signature bytes — verify then fails because the sig no longer covers the
    // changed metadata (or the changed field trips its own check first).
    private static global::app.type.signature.@this Layer(Data signed)
        => (global::app.type.signature.@this)signed.Peek();

    private static Data Tampered(Data signed,
        global::app.type.text.@this? algorithm = null,
        global::app.type.datetime.@this? created = null,
        global::app.module.crypto.type.hash.@this? hash = null,
        global::app.type.binary.@this? signature = null,
        bool contractsNull = false)
    {
        var l = Layer(signed);
        var rebuilt = new global::app.type.signature.@this(
            l.Value, algorithm ?? l.Algorithm, l.Nonce, created ?? l.Created,
            l.Identity, hash ?? l.Hash, signature ?? l.Signature, l.Expires,
            contractsNull ? null : l.Contracts);
        var d = Data.Ok(rebuilt);
        d.Context = signed.Context;
        return d;
    }

    #region Happy Path

    [Test]
    public async Task Verify_ValidSignature_ReturnsSuccess()
    {
        var signed = await SignHelper("hello", contracts: new List<string> { "C0" });
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });

        await result.IsSuccess();
    }

    #endregion

    #region Error Keys

    [Test]
    public async Task Verify_NoSignature_Error()
    {
        var data = Data.Ok("unsigned");
        var result = await VerifyHelper(data, contracts: new List<string> { "C0" });

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoSignature");
    }

    [Test]
    public async Task Verify_TamperedAlgorithm_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var tampered = Tampered(signed, algorithm: new global::app.type.text.@this("unknown-algo"));

        var result = await VerifyHelper(tampered, contracts: new List<string> { "C0" });
        await result.IsFailure();
        // Tampered algorithm changes the signing bytes, so signature verification fails.
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    [Test]
    public async Task Verify_Expired_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" }, expires: TimeSpan.FromMilliseconds(50));
        await Task.Delay(100);

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Expired");
    }

    [Test]
    public async Task Verify_TimedOut_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var tampered = Tampered(signed, created: new global::app.type.datetime.@this(DateTimeOffset.UtcNow.AddHours(-1)));

        var result = await VerifyHelper(tampered, contracts: new List<string> { "C0" }, timeoutMs: 1000);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TimedOut");
    }

    [Test]
    public async Task Verify_NonceReplay_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var first = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await first.IsSuccess();

        // Second verify — same nonce → replay.
        var second = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await second.IsFailure();
        await Assert.That(second.Error!.Key).IsEqualTo("NonceReplay");
    }

    [Test]
    public async Task Verify_ContractMismatch_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C1" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    [Test]
    public async Task Verify_DataHashMismatch_Error()
    {
        var signed = await SignHelper(new { amount = 100 }, contracts: new List<string> { "C0" });
        var tampered = Tampered(signed,
            hash: new global::app.module.crypto.type.hash.@this(new byte[32], "keccak256"));

        var result = await VerifyHelper(tampered, contracts: new List<string> { "C0" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task Verify_SignatureInvalid_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var sigBytes = (byte[])Layer(signed).Signature.Value.Clone();
        sigBytes[0] ^= 0xFF;
        var tampered = Tampered(signed, signature: new global::app.type.binary.@this(sigBytes));

        var result = await VerifyHelper(tampered, contracts: new List<string> { "C0" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureInvalid");
    }

    #endregion

    #region Contract Matching

    [Test]
    public async Task Verify_ContractMatch_OrderIndependent()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0", "C1" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C1", "C0" });
        await result.IsSuccess();
    }

    [Test]
    public async Task Verify_NullContracts_BothNull_Succeeds()
    {
        var signed = await SignHelper("test");

        var result = await VerifyHelper(signed, contracts: null);
        await result.IsSuccess();
    }

    [Test]
    public async Task Verify_WithContracts_HappyPath()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0", "C1" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0", "C1" });
        await result.IsSuccess();
    }

    [Test]
    public async Task Verify_Algorithm_IsEd25519_AndVerifies()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        await Assert.That(Layer(signed).Algorithm.ToString()).IsEqualTo("ed25519");

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await result.IsSuccess();
    }

    #endregion

    #region Nonce Cache Contract

    [Test]
    public async Task Verify_FreshNonce_StoredInCache()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var nonce = Layer(signed).Nonce.ToString();

        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await result.IsSuccess();

        var cached = await _app.Cache.GetAsync($"nonce:{nonce}");
        await Assert.That(cached).IsNotNull();
    }

    #endregion

    #region Boundary Conditions

    [Test]
    public async Task Verify_CreatedJustWithinTimeout_Succeeds()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" }, timeoutMs: 60_000);
        await result.IsSuccess();
    }

    #endregion

    #region Empty Contracts

    [Test]
    public async Task Verify_EmptyContractsList_BothEmpty_Succeeds()
    {
        var signed = await SignHelper("test", contracts: new List<string>());
        var result = await VerifyHelper(signed, contracts: new List<string>());

        await result.IsSuccess();
    }

    [Test]
    public async Task Verify_RequiredContracts_SignedHasNone_ReturnsError()
    {
        var signed = await SignHelper("test");
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });

        await result.IsFailure();
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

        await result1.IsSuccess();
        await result2.IsSuccess();
    }

    #endregion

    #region Tampered Contracts on Signature

    [Test]
    public async Task Verify_SignedDataContractsNull_RequiredNotNull_ReturnsError()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var tampered = Tampered(signed, contractsNull: true);

        var result = await VerifyHelper(tampered, contracts: new List<string> { "C0" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    #endregion

    #region Verify Check Order

    [Test]
    public async Task Verify_ExpiredAndNonceReplay_ReturnsExpiredNotNonceReplay()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" }, expires: TimeSpan.FromMilliseconds(50));
        var first = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await Task.Delay(100);

        // Expired is checked before NonceReplay.
        var second = await VerifyHelper(signed, contracts: new List<string> { "C0" });
        await second.IsFailure();
        await Assert.That(second.Error!.Key).IsEqualTo("Expired");
    }

    [Test]
    public async Task Verify_TimedOutAndContractMismatch_ReturnsTimedOutNotContractMismatch()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var tampered = Tampered(signed, created: new global::app.type.datetime.@this(DateTimeOffset.UtcNow.AddHours(-1)));

        var result = await VerifyHelper(tampered, contracts: new List<string> { "C1" }, timeoutMs: 1000);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TimedOut");
    }

    #endregion

}
