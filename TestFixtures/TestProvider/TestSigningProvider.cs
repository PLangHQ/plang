using app.module.code;
using app.module.signing;
using app.module.signing.code;

namespace TestProvider;

/// <summary>
/// A minimal ISigning for testing the provider load action.
/// Must have a parameterless constructor and implement ISigning.
/// </summary>
public class TestSigningProvider : ISigning
{
    public string Name => "test-signing";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (KeyPair? keys, app.error.@this? error) GenerateKeyPair()
        => (new KeyPair("testPub", "testPriv"), null);

    // signs and verifies as the built-in does: an app whose default this is still signs what it takes (its snapshots)
    public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey)
        => new Ed25519().Sign(unsigned, privateKey);

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
        => new Ed25519().Verify(signature);

    public Task<app.data.@this> SignAsync(sign action)
        => new Ed25519().SignAsync(action);

    public Task<app.data.@this<global::app.type.item.@bool.@this>> VerifyAsync(verify action)
        => new Ed25519().VerifyAsync(action);
}
