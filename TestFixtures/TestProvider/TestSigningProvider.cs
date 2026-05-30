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

    public app.data.@this<KeyPair> GenerateKeyPair()
        => app.data.@this<KeyPair>.Ok(new KeyPair("testPub", "testPriv"));

    public app.data.@this<byte[]> Sign(byte[] data, string privateKey)
        => app.data.@this<byte[]>.Ok(new byte[64]);

    public app.data.@this<bool> Verify(byte[] data, byte[] signature, string publicKey)
        => app.data.@this<bool>.Ok(true);

    public Task<app.data.@this<object>> SignAsync(sign action)
        => Task.FromResult(new app.data.@this<object>());

    public Task<app.data.@this<bool>> VerifyAsync(verify action)
        => Task.FromResult(app.data.@this<bool>.Ok(true));
}
