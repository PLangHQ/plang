namespace PLang.Tests.Shared;

/// <summary>
/// Minimal <see cref="global::app.module.signing.code.ISigning"/> mock for the C# test
/// suites — skips the real ed25519 keygen, keccak256 hashing and signing entirely
/// (those dominate test wall-clock now that born-with-context lets the sign path run).
///
/// It still produces the real signature-LAYER shape (so wire-shape / round-trip tests
/// pass) but with constant fake crypto, and verify always succeeds. Registered as the
/// default <c>ISigning</c> by <see cref="global::PLang.Tests.AppTesting.Testing"/>. Tests
/// that exercise REAL signing/verification construct a plain <c>app.@this</c> (real Ed25519).
/// </summary>
public sealed class TestSigning : global::app.module.signing.code.ISigning
{
    public string Name => "test-signing";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (global::app.module.signing.code.KeyPair? keys, global::app.error.@this? error) GenerateKeyPair()
        => (new global::app.module.signing.code.KeyPair("test-public-key", "test-private-key"), null);

    public async Task<global::app.data.@this> SignAsync(global::app.module.signing.sign action)
    {
        // the data as the view it is signed in takes it, as the real signer settles it
        var view = action.View;
        var data = await action.Data!.Settle(view);
        var unsigned = new global::app.type.item.signature.@this(
            type: action.Context.App.type.list[new global::app.type.@this("signature", Name), action.Context],
            value: data,
            algorithm: action.Context.Text(Name),
            nonce: action.Context.Text("test-nonce"),
            created: new global::app.type.item.datetime.@this(System.DateTimeOffset.FromUnixTimeSeconds(0),
                action.Context.App.type.list[typeof(global::app.type.item.datetime.@this)]),
            identity: action.Context.Text("test-public-key"),
            hash: new global::app.module.crypto.type.hash.@this(System.Array.Empty<byte>(),
                (global::app.module.crypto.type.hash.kind.@this)action.Context.App.type.list[typeof(global::app.module.crypto.type.hash.@this)].kind["keccak256"]!),
            signature: new global::app.type.item.binary.@this(System.Array.Empty<byte>(), action.Context.App.type.list[typeof(global::app.type.item.binary.@this)]),
            view: view);
        // the Data it settled, holding its layer — as the real signer answers
        return data.Copy(unsigned);
    }

    // The crypto is faked, but the Data is read as the real verifier reads it: one that holds no signature is refused.
    public async Task<global::app.data.@this<global::app.type.item.@bool.@this>> VerifyAsync(global::app.module.signing.verify action)
    {
        if ((action.Data == null ? null : await action.Data.Follow(action.Context))?.Signature == null)
            return action.Context.Error<global::app.type.item.@bool.@this>(new global::app.error.@this("Data has no signature", "NoSignature", 400));
        return action.Context.Ok<global::app.type.item.@bool.@this>(new global::app.type.item.@bool.@this(true,
            action.Context.App.type.list[typeof(global::app.type.item.@bool.@this)]));
    }

    public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey)
        => new global::app.type.item.binary.@this(System.Array.Empty<byte>(), unsigned.Signature.Type);

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
        => global::app.type.item.@bool.@this.True;
}
