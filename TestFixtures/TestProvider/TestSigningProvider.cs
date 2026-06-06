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

    public app.data.@this<global::app.type.binary.@this> Sign(byte[] data, string privateKey)
        => app.data.@this<global::app.type.binary.@this>.Ok(new byte[64]);

    public app.data.@this<global::app.type.@bool.@this> Verify(byte[] data, byte[] signature, string publicKey)
        => app.data.@this<global::app.type.@bool.@this>.Ok(true);

    public Task<app.data.@this<object>> SignAsync(sign action)
        => Task.FromResult(new app.data.@this<object>());

    public Task<app.data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action)
        => Task.FromResult(app.data.@this<global::app.type.@bool.@this>.Ok(true));
}
