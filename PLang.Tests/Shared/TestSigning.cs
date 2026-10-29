namespace PLang.Tests.Shared;

/// <summary>
/// Minimal <see cref="global::app.module.signing.code.ISigning"/> mock for the C# test
/// suites — skips the real ed25519 keygen and signing entirely (those dominate test
/// wall-clock now that born-with-context lets the sign path run).
///
/// It produces the real signature-LAYER shape (so wire-shape / round-trip tests pass), and it
/// binds the value as the real signer does: the digest of the Data in the view its signature
/// re-hashes it in, re-hashed at verify. So a tamper or an edit is refused here as with the real
/// signer (<c>DataHashMismatch</c>). The keys and signature bytes stay faked: a test of those
/// (<c>SignatureInvalid</c>, another identity) constructs a plain <c>app.@this</c> (real Ed25519).
/// Registered as the default <c>ISigning</c> by <see cref="global::PLang.Tests.AppTesting.Testing"/>.
/// </summary>
public sealed class TestSigning : global::app.module.signing.code.ISigning
{
    public string Name => "test-signing";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (global::app.module.signing.code.KeyPair? keys, global::app.error.@this? error) GenerateKeyPair()
        => (new global::app.module.signing.code.KeyPair("test-public-key", "test-private-key"), null);

    // the kind the mock hashes by: sha256, cheaper than the real setting's default
    private static global::app.module.crypto.type.hash.kind.@this Sha256(global::app.actor.context.@this context)
        => (global::app.module.crypto.type.hash.kind.@this)context.App.type.list[typeof(global::app.module.crypto.type.hash.@this)].kind["sha256"]!;

    public async Task<global::app.data.@this> SignAsync(global::app.module.signing.sign action)
    {
        // the data as the view it is signed in takes it, and its digest in the view its signature re-hashes it in — as
        // the real signer settles and hashes it
        var data = await action.Data!.Settle(action.View ?? global::app.View.Out);
        var view = action.View ?? data.Type?.View ?? global::app.View.Out;
        var unsigned = new global::app.type.item.signature.@this(
            type: action.Context.App.type.list[new global::app.type.@this("signature", Name), action.Context],
            value: data,
            algorithm: action.Context.Text(Name),
            nonce: action.Context.Text("test-nonce"),
            created: new global::app.type.item.datetime.@this(System.DateTimeOffset.FromUnixTimeSeconds(0),
                action.Context.App.type.list[typeof(global::app.type.item.datetime.@this)]),
            identity: action.Context.Text(TestIdentity.PublicKey),
            hash: await data.Digest(view, Sha256(action.Context), action.Context),
            signature: new global::app.type.item.binary.@this(System.Array.Empty<byte>(), action.Context.App.type.list[typeof(global::app.type.item.binary.@this)]));
        // the Data it settled, holding its layer — as the real signer answers
        return data.Copy(unsigned);
    }

    // The crypto is faked, but the Data is read as the real verifier reads it: one that holds no signature is refused, and
    // one whose value is no longer what was signed — re-hashed in the view it was signed in — is refused.
    public async Task<global::app.data.@this<global::app.type.item.@bool.@this>> VerifyAsync(global::app.module.signing.verify action)
    {
        var bearer = action.Data == null ? null : await action.Data.Follow(action.Context);
        if (bearer?.Signature is not { } signature)
            return action.Context.Error<global::app.type.item.@bool.@this>(new global::app.error.@this("Data has no signature", "NoSignature", 400));
        var over = signature.Over(bearer);
        if (signature.Hash.Algorithm is not { } kind
            || !(await over.Value.Digest(over.Hashed, kind, action.Context)).DigestEquals(signature.Hash))
            return action.Context.Error<global::app.type.item.@bool.@this>(new global::app.error.@this("Data hash does not match signed hash", "DataHashMismatch", 400));
        return action.Context.Ok<global::app.type.item.@bool.@this>(new global::app.type.item.@bool.@this(true,
            action.Context.App.type.list[typeof(global::app.type.item.@bool.@this)]));
    }

    public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey)
        => new global::app.type.item.binary.@this(System.Array.Empty<byte>(), unsigned.Signature.Type);

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
        => global::app.type.item.@bool.@this.True;
}
