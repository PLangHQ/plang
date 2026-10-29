using app.actor.context;
using app.error;
using app.type.item.variable;
using app.module.code;
using app.module.signing.code;
using app.module.crypto;
using app.module.identity;
using app.module.signing;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.signing;

/// <summary>
/// Tests the verify action handler.
/// Verify checks in order: NoSignature → Expired → ContractMismatch → DataHashMismatch → SignatureInvalid → NonceReplay (a live read only)
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
        _app = new PLangEngine(_tempDir, global::PLang.Tests.Parent.Actor);
        (_app).TestIdentity();
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

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    private async Task<Data> SignHelper(object data, List<string>? contracts = null,
        TimeSpan? expires = null, Dictionary<string, object>? headers = null)
    {
        var action = new sign(Ctx) { Data = new Data("", data, context: Ctx),
            Contracts = contracts is null ? null : new global::app.data.@this<global::app.type.item.list.@this>("", global::PLang.Tests.Shared.Make.List(contracts, Ctx), context: Ctx),
            Expires = expires.HasValue ? Ctx.Ok<global::app.type.item.duration.@this>((global::app.type.item.duration.@this)expires.Value) : null,
        };
        return await new global::app.goal.step.action.composed.@this(action, Ctx).Start(Ctx);
    }

    private async Task<Data> VerifyHelper(Data signedData, List<string>? contracts = null,
        Dictionary<string, object>? headers = null)
    {
        var action = new verify(Ctx) { Data = signedData,
            Contracts = contracts is null ? null : new global::app.data.@this<global::app.type.item.list.@this>("", global::PLang.Tests.Shared.Make.List(contracts, Ctx), context: Ctx),
        };
        return await new global::app.goal.step.action.composed.@this(action, Ctx).Start(Ctx);
    }

    // A signature's contracts cross as bare strings, in their slot of the layer: ["C0"], never Data records.
    [Test]
    public async Task ASignaturesContracts_CrossAsBareStrings()
    {
        var signed = await SignHelper("hello", ["C0"]);
        await signed.IsSuccess();
        using var stream = new System.IO.MemoryStream();
        await (await _app.type.list.Mime("application/plang").Encode(stream, signed, Ctx, global::app.View.Out)).IsSuccess();
        var wire = System.Text.Json.Nodes.JsonNode.Parse(stream.ToArray())!;
        await Assert.That(wire["contracts"]!.ToJsonString()).IsEqualTo("[\"C0\"]");
    }

    // sign answers the Data it signed, holding its layer (immutable). To
    // "tamper", rebuild the layer with one field changed but the ORIGINAL
    // signature bytes — verify then fails because the sig no longer covers the
    // changed metadata (or the changed field trips its own check first).
    private static global::app.type.item.signature.@this Layer(Data signed)
        => signed.Signature!;

    private static Data Tampered(Data signed,
        global::app.type.item.text.@this? algorithm = null,
        global::app.type.item.datetime.@this? created = null,
        global::app.module.crypto.type.hash.@this? hash = null,
        global::app.type.item.binary.@this? signature = null,
        bool contractsNull = false,
        TimeSpan? window = null)
    {
        var l = Layer(signed);
        var rebuilt = new global::app.type.item.signature.@this(
            l.Type, l.Value, algorithm ?? l.Algorithm, l.Nonce, created ?? l.Created,
            l.Identity, hash ?? l.Hash, signature ?? l.Signature, l.Expires,
            contractsNull ? null : l.Contracts,
            window is null ? global::app.type.item.signature.Origin.Signed : global::app.type.item.signature.Origin.Live,
            window is { } w ? signed.Context.Duration(w) : null);
        return signed.Copy(rebuilt);
    }

    // The signature as a live read off the wire has it: born with a window (the wire reader gives the signing
    // setting's expiry).
    private static Data Live(Data signed, TimeSpan? window = null, global::app.type.item.datetime.@this? created = null)
        => Tampered(signed, created: created, window: window ?? TimeSpan.FromMinutes(5));

    #region Happy Path

    [Test]
    public async Task Verify_ValidSignature_ReturnsSuccess()
    {
        var signed = await SignHelper("hello", contracts: new List<string> { "C0" });
        var result = await VerifyHelper(signed, contracts: new List<string> { "C0" });

        await result.IsSuccess();
    }

    #endregion

    // Through variables, as a program writes it: `sign %data%, write to %signed%`, then `verify %signed%`. The action's
    // Data is the property as written — a reference — so each must read what it names.
    private async Task<Data> Program(string module, string action, params (string, object?)[] properties)
        => await global::PLang.Tests.Shared.Make.Action(Ctx, module, action, properties).Start(Ctx);

    [Test]
    public async Task ASignatureKeptInAVariable_Verifies()
    {
        await Ctx.Variable.Set("data", "hello");
        var signed = await Program("signing", "sign", ("Data", "%data%"));
        await signed.IsSuccess();
        await Ctx.Variable.Set("signed", signed);

        var verified = await Program("signing", "verify", ("Data", "%signed%"));

        await verified.IsSuccess();
        await Assert.That((await verified.Value())?.ToString()).IsEqualTo("true");
    }

    // A signature binds the value it signed: the variable it was signed from changing afterwards changes nothing it holds.
    [Test]
    public async Task ASignature_HoldsTheValueItSigned_NotTheVariable()
    {
        await Ctx.Variable.Set("data", "hello");
        var signed = await Program("signing", "sign", ("Data", "%data%"));
        await signed.IsSuccess();

        await Ctx.Variable.Set("data", "changed");

        await Assert.That((await signed.Value())?.ToString()).IsEqualTo("hello");
        await Ctx.Variable.Set("signed", signed);
        await (await Program("signing", "verify", ("Data", "%signed%"))).IsSuccess();
    }

    // The test suites' fake signer reads its Data as the real one does: a value with no signature is refused, and a
    // signature kept in a variable verifies — so a reference read without what it names can't hide behind it.
    [Test]
    public async Task TheTestSigner_ReadsItsData()
    {
        await using var app = new global::app.@this(_tempDir, global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.System.Context;
        await ctx.Variable.Set("plain", "hello");
        var refused = await global::PLang.Tests.Shared.Make.Action(ctx, "signing", "verify", ("Data", "%plain%")).Start(ctx);
        await Assert.That(refused.Error?.Key).IsEqualTo("NoSignature");

        await ctx.Variable.Set("data", "hello");
        await ctx.Variable.Set("signed", await global::PLang.Tests.Shared.Make.Action(ctx, "signing", "sign", ("Data", "%data%")).Start(ctx));
        await (await global::PLang.Tests.Shared.Make.Action(ctx, "signing", "verify", ("Data", "%signed%")).Start(ctx)).IsSuccess();
    }

    #region A signed value is the Data it signed

    // Reads %path% as a program does.
    private async Task<Data> Read(string path)
        => await new global::app.type.item.variable.@this(path, _app.type.list).Start(Ctx);

    private async Task<Data> SignedOrder()
    {
        await Ctx.Variable.Set("order", new Dictionary<string, object?> { ["total"] = 5L, ["message"] = "signed" });
        var signed = await Program("signing", "sign", ("Data", "%order%"));
        await signed.IsSuccess();
        await Ctx.Variable.Set("signed", signed);
        return signed;
    }

    // In memory as off the wire: %signed.total% is the signed order's total, and %signed!signature% its layer.
    [Test]
    public async Task ASignedValue_ReadsAsTheDataItSigned()
    {
        await SignedOrder();

        await Assert.That((await (await Read("signed.total")).Value())?.ToString()).IsEqualTo("5");
        await Assert.That((await Read("signed!signature")).Peek()).IsTypeOf<global::app.type.item.signature.@this>();
    }

    // %x!signature.identity% names the signer only of a Data its signature verifies: a member written after signing
    // keeps the signature, but the identity read is verify's error, never the name.
    [Test]
    public async Task TheSignersIdentity_OfADataChangedAfterSigning_IsTheVerifysError()
    {
        await SignedOrder();
        await new global::app.type.item.variable.@this("signed.message", _app.type.list).Set("changed", Ctx);

        var identity = await Read("signed!signature.identity");
        await identity.IsFailure();
        await Assert.That(identity.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task TheSignersIdentity_OfAnUntouchedSignedData_IsItsName()
    {
        await SignedOrder();
        var identity = await Read("signed!signature.identity");
        await identity.IsSuccess();
        await Assert.That((await identity.Value())?.ToString()).IsNotEmpty();
    }

    // A member written after signing is a different Data: verify names it (the digest no longer matches), the write
    // doesn't drop the signature.
    [Test]
    public async Task AMemberWrittenAfterSigning_DataHashMismatch()
    {
        await SignedOrder();
        await new global::app.type.item.variable.@this("signed.message", _app.type.list).Set("changed", Ctx);

        var result = await Program("signing", "verify", ("Data", "%signed%"));
        await Assert.That(result.Error?.Key).IsEqualTo("DataHashMismatch");
    }

    // Intended: the digest covers the Data's properties, so a property added after signing fails verify too.
    [Test]
    public async Task APropertyAddedAfterSigning_DataHashMismatch()
    {
        await SignedOrder();
        await new global::app.type.item.variable.@this("signed!note", _app.type.list).Set("added", Ctx);

        var result = await Program("signing", "verify", ("Data", "%signed%"));
        await Assert.That(result.Error?.Key).IsEqualTo("DataHashMismatch");
    }

    // A signed value kept in a list is the same signed Data: its row verifies.
    [Test]
    public async Task ASignedValue_InAList_Verifies()
    {
        await SignedOrder();
        await Ctx.Variable.Set("list", new List<object?>());
        await (await global::PLang.Tests.Shared.Make.Action(Ctx, "list", "add", ("Value", "%signed%"),
            global::PLang.Tests.Shared.Make.Param(Ctx, "ListName", "%list%", "variable")).Start(Ctx)).IsSuccess();

        await (await Program("signing", "verify", ("Data", "%list[0]%"))).IsSuccess();
    }

    // A property a value carries (`set %order!note% = …`) stays on it in a list.
    [Test]
    public async Task AProperty_SurvivesTheAdd()
    {
        await Ctx.Variable.Set("order", new Dictionary<string, object?> { ["total"] = 5L });
        await new global::app.type.item.variable.@this("order!note", _app.type.list).Set("kept", Ctx);
        await Ctx.Variable.Set("orders", new List<object?>());
        await (await global::PLang.Tests.Shared.Make.Action(Ctx, "list", "add", ("Value", "%order%"),
            global::PLang.Tests.Shared.Make.Param(Ctx, "ListName", "%orders%", "variable")).Start(Ctx)).IsSuccess();

        await Assert.That((await (await Read("orders[0]!note")).Value())?.ToString()).IsEqualTo("kept");
    }

    // A member kept beside a value it has no place in (`set %name.lang% = "is"` on a text) stays with it in a list.
    [Test]
    public async Task AKeptMember_SurvivesTheAdd()
    {
        await Ctx.Variable.Set("name", "Ada");
        await new global::app.type.item.variable.@this("name.lang", _app.type.list).Set("is", Ctx);
        await Ctx.Variable.Set("names", new List<object?>());
        await (await global::PLang.Tests.Shared.Make.Action(Ctx, "list", "add", ("Value", "%name%"),
            global::PLang.Tests.Shared.Make.Param(Ctx, "ListName", "%names%", "variable")).Start(Ctx)).IsSuccess();

        await Assert.That((await (await Read("names[0].lang")).Value())?.ToString()).IsEqualTo("is");
    }

    // In memory a row keeps its signature; across actors a list rides in ONE signature that attests the whole, and its
    // rows arrive as values — a row read off the wire has no signature of its own, the list's verify is what passes.
    [Test]
    public async Task AListAcrossTheWire_IsSignedWhole_ItsRowsAreValues()
    {
        await SignedOrder();
        await Ctx.Variable.Set("orders", new List<object?>());
        await (await global::PLang.Tests.Shared.Make.Action(Ctx, "list", "add", ("Value", "%signed%"),
            global::PLang.Tests.Shared.Make.Param(Ctx, "ListName", "%orders%", "variable")).Start(Ctx)).IsSuccess();
        var plang = _app.type.list.Mime("application/plang");
        using var wire = new System.IO.MemoryStream();
        await (await plang.Encode(wire, await Read("orders"), Ctx, global::app.View.Out)).IsSuccess();

        var arrived = await plang.Decode(wire.ToArray(), Ctx, global::app.View.Out);
        await arrived.IsSuccess();
        await Ctx.Variable.Set("arrived", arrived);

        await Assert.That((await Program("signing", "verify", ("Data", "%arrived[0]%"))).Error?.Key).IsEqualTo("NoSignature");
    }

    // A changed bearer crosses with its old layer — never re-signed on the way out — and the receiver refuses it.
    [Test]
    public async Task AChangedSignedValue_IsRefusedWhereItArrives()
    {
        await SignedOrder();
        await new global::app.type.item.variable.@this("signed.message", _app.type.list).Set("changed", Ctx);
        var plang = _app.type.list.Mime("application/plang");
        using var wire = new System.IO.MemoryStream();
        await (await plang.Encode(wire, await Read("signed"), Ctx, global::app.View.Out)).IsSuccess();

        var arrived = await plang.Decode(wire.ToArray(), Ctx, global::app.View.Out);

        await Assert.That(arrived.Error?.Key).IsEqualTo("DataHashMismatch");
    }

    // A signature binds the content, not the name it is bound under: one kept in plang's own store, read back and bound
    // as another variable, still verifies.
    [Test]
    public async Task AStoredSignedValue_BoundUnderAnotherName_Verifies()
    {
        // the store's write signs it, in the store's view
        var value = new Data("row", global::app.type.item.@this.Create(new Dictionary<string, object?> { ["total"] = 5L }, Ctx), context: Ctx);
        var plang = _app.type.list.Mime("application/plang");
        using var store = new System.IO.MemoryStream();
        await (await plang.Encode(store, value, Ctx, global::app.View.Store)).IsSuccess();
        var read = await plang.Decode(store.ToArray(), Ctx, global::app.View.Store);
        await read.IsSuccess();
        await Ctx.Variable.Set("row", read);

        await (await Program("signing", "verify", ("Data", "%row%"))).IsSuccess();
    }

    #endregion

    #region Error Keys

    [Test]
    public async Task Verify_NoSignature_Error()
    {
        var data = Ctx.Ok("unsigned");
        var result = await VerifyHelper(data, contracts: new List<string> { "C0" });

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoSignature");
    }

    [Test]
    public async Task Verify_TamperedAlgorithm_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var tampered = Tampered(signed, algorithm: Ctx.Text("unknown-algo"));

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

    // A live read past its window has expired, though its signer signed no expiry.
    [Test]
    public async Task Verify_LiveReadPastItsWindow_Expired()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var live = Live(signed, window: TimeSpan.FromSeconds(1),
            created: new global::app.type.item.datetime.@this(DateTimeOffset.UtcNow.AddHours(-1), _app.type.list[typeof(global::app.type.item.datetime.@this)]));

        var result = await VerifyHelper(live, contracts: new List<string> { "C0" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Expired");
    }

    [Test]
    public async Task Verify_NonceReplay_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var first = await VerifyHelper(Live(signed), contracts: new List<string> { "C0" });
        await first.IsSuccess();

        // The same signature arriving again — a second live read, same nonce → replay.
        var second = await VerifyHelper(Live(signed), contracts: new List<string> { "C0" });
        await second.IsFailure();
        await Assert.That(second.Error!.Key).IsEqualTo("NonceReplay");
    }

    // A signature not read live (from plang's own store) presents the same nonce on every read: not replay.
    [Test]
    public async Task Verify_NotLive_SameNonceAgain_Succeeds()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        await (await VerifyHelper(signed, contracts: new List<string> { "C0" })).IsSuccess();
        await (await VerifyHelper(signed, contracts: new List<string> { "C0" })).IsSuccess();
    }

    // A signature read live is born with its window: one without is refused at construction.
    [Test]
    public async Task ALiveSignatureWithNoWindow_IsRefused()
    {
        var l = Layer(await SignHelper("test"));

        var refused = Assert.Throws<global::app.error.AppException>(() => new global::app.type.item.signature.@this(
            l.Type, l.Value, l.Algorithm, l.Nonce, l.Created, l.Identity, l.Hash, l.Signature, null, l.Contracts,
            global::app.type.item.signature.Origin.Live, window: null));

        await Assert.That(refused!.Error.Key).IsEqualTo("LiveSignatureNeedsWindow");
    }

    // Expires is the earlier of what the signer signed and, read live, the window after Created.
    [Test]
    public async Task Expires_IsTheEarlierOfSignedAndTheLiveWindow()
    {
        var signedLong = await SignHelper("test", expires: TimeSpan.FromHours(1));
        var live = Layer(Live(signedLong, window: TimeSpan.FromMinutes(5)));
        await Assert.That(live.Expires!.Value).IsEqualTo(live.Created.Value + TimeSpan.FromMinutes(5));

        var signedShort = await SignHelper("test", expires: TimeSpan.FromMinutes(1));
        var liveShort = Layer(Live(signedShort, window: TimeSpan.FromMinutes(5)));
        await Assert.That(liveShort.Expires!.Value).IsEqualTo(Layer(signedShort).Expires!.Value);

        await Assert.That(Layer(await SignHelper("test")).Expires).IsNull();
    }

    // %x!signature.expired% asks the signature against the program's clock.
    [Test]
    public async Task Expired_IsReadAsAMember()
    {
        var signed = await SignHelper("test");
        var past = Live(signed, window: TimeSpan.FromSeconds(1),
            created: new global::app.type.item.datetime.@this(DateTimeOffset.UtcNow.AddHours(-1), _app.type.list[typeof(global::app.type.item.datetime.@this)]));
        await Ctx.Variable.Set("past", past);
        await Ctx.Variable.Set("live", Live(signed));
        await Assert.That((await (await Read("past!signature.expired")).Value())?.ToString()).IsEqualTo("true");
        await Assert.That((await (await Read("live!signature.expired")).Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Verify_ContractMismatch_Error()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });

        var result = await VerifyHelper(signed, contracts: new List<string> { "C1" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ContractMismatch");
    }

    // The digest covers the Data's properties: the held Data with one property changed (same value, same signature
    // bytes) no longer matches its hash.
    [Test]
    public async Task Verify_HeldDataPropertyChanged_DataHashMismatch()
    {
        var data = new Data("", "hello", context: Ctx);
        data.Properties.Set("note", "signed");
        var signed = await new global::app.goal.step.action.composed.@this(new sign(Ctx) { Data = data }, Ctx).Start(Ctx);
        await signed.IsSuccess();
        var l = Layer(signed);
        var changed = new Data("", l.Value.Peek(), context: Ctx);
        changed.Properties.Set("note", "changed");
        var tampered = changed.Copy(l);

        var result = await VerifyHelper(tampered);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task Verify_DataHashMismatch_Error()
    {
        var signed = await SignHelper(new { amount = 100 }, contracts: new List<string> { "C0" });
        var tampered = Tampered(signed,
            hash: new global::app.module.crypto.type.hash.@this(new byte[32], (global::app.module.crypto.type.hash.kind.keccak256.@this)_app.type.list[typeof(global::app.module.crypto.type.hash.@this)].kind["keccak256"]!));

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
        var tampered = Tampered(signed, signature: new global::app.type.item.binary.@this(sigBytes, _app.type.list[typeof(global::app.type.item.binary.@this)]));

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

        var result = await VerifyHelper(Live(signed), contracts: new List<string> { "C0" });
        await result.IsSuccess();

        var cached = await _app.Cache.GetAsync($"nonce:{nonce}");
        await Assert.That(cached).IsNotNull();
    }

    #endregion

    #region Boundary Conditions

    [Test]
    public async Task Verify_LiveReadWithinItsWindow_Succeeds()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var result = await VerifyHelper(Live(signed, window: TimeSpan.FromMinutes(1)), contracts: new List<string> { "C0" });
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
    public async Task Verify_LiveExpiredAndContractMismatch_ReturnsExpiredNotContractMismatch()
    {
        var signed = await SignHelper("test", contracts: new List<string> { "C0" });
        var live = Live(signed, window: TimeSpan.FromSeconds(1),
            created: new global::app.type.item.datetime.@this(DateTimeOffset.UtcNow.AddHours(-1), _app.type.list[typeof(global::app.type.item.datetime.@this)]));

        var result = await VerifyHelper(live, contracts: new List<string> { "C1" });
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Expired");
    }

    #endregion

}
