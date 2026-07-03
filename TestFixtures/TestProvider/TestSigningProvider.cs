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
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (KeyPair? keys, app.error.IError? error) GenerateKeyPair()
        => (new KeyPair("testPub", "testPriv"), null);

    public global::app.type.binary.@this Sign(global::app.type.signature.@this unsigned, global::app.type.text.@this privateKey)
        => new global::app.type.binary.@this(new byte[64]);

    public global::app.type.@bool.@this Verify(global::app.type.signature.@this signature)
        => new global::app.type.@bool.@this(true);

    public Task<app.data.@this> SignAsync(sign action)
        => Task.FromResult(app.data.@this.Ok());

    public Task<app.data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action)
        => Task.FromResult(app.data.@this<global::app.type.@bool.@this>.Ok(true));
}
