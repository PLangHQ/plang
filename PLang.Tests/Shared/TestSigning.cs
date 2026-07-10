namespace PLang.Tests.Shared;

/// <summary>
/// Minimal <see cref="global::app.module.signing.code.ISigning"/> mock for the C# test
/// suites — skips the real ed25519 keygen, keccak256 hashing and signing entirely
/// (those dominate test wall-clock now that born-with-context lets the sign path run).
///
/// It still produces the real signature-LAYER shape (so wire-shape / round-trip tests
/// pass) but with constant fake crypto, and verify always succeeds. Registered as the
/// default <c>ISigning</c> by <see cref="global::PLang.Tests.TestApp.Create"/>. Tests
/// that exercise REAL signing/verification construct a plain <c>app.@this</c> (real Ed25519).
/// </summary>
public sealed class TestSigning : global::app.module.signing.code.ISigning
{
    public string Name => "test-signing";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (global::app.module.signing.code.KeyPair? keys, global::app.error.IError? error) GenerateKeyPair()
        => (new global::app.module.signing.code.KeyPair("test-public-key", "test-private-key"), null);

    public Task<global::app.data.@this> SignAsync(global::app.module.signing.sign action)
    {
        var unsigned = new global::app.type.item.signature.@this(
            value: action.Data!,
            algorithm: new global::app.type.item.text.@this(Name),
            nonce: new global::app.type.item.text.@this("test-nonce"),
            created: new global::app.type.item.datetime.@this(System.DateTimeOffset.FromUnixTimeSeconds(0)),
            identity: new global::app.type.item.text.@this("test-public-key"),
            hash: new global::app.module.crypto.type.hash.@this(System.Array.Empty<byte>(), "test"),
            signature: new global::app.type.item.binary.@this(System.Array.Empty<byte>()));
        return Task.FromResult(action.Context.Ok((object?)unsigned));
    }

    public Task<global::app.data.@this<global::app.type.item.@bool.@this>> VerifyAsync(global::app.module.signing.verify action)
        => Task.FromResult(action.Context.Ok<global::app.type.item.@bool.@this>(new global::app.type.item.@bool.@this(true)));

    public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey)
        => new global::app.type.item.binary.@this(System.Array.Empty<byte>());

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
        => new global::app.type.item.@bool.@this(true);
}
