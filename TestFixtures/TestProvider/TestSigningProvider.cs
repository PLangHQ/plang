using app.Code;
using app.modules.signing;
using app.modules.signing.code;

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

    public app.Data.@this<KeyPair> GenerateKeyPair()
        => app.Data.@this<KeyPair>.Ok(new KeyPair("testPub", "testPriv"));

    public app.Data.@this Sign(byte[] data, string privateKey)
        => app.Data.@this.Ok(new byte[64]);

    public app.Data.@this Verify(byte[] data, byte[] signature, string publicKey)
        => app.Data.@this.Ok(true);

    public Task<app.Data.@this> SignAsync(sign action)
        => Task.FromResult(app.Data.@this.Ok());

    public Task<app.Data.@this> VerifyAsync(verify action)
        => Task.FromResult(app.Data.@this.Ok(true));
}
