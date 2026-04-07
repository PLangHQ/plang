using App.Providers;
using App.modules.signing;
using App.modules.signing.providers;

namespace TestProvider;

/// <summary>
/// A minimal ISigningProvider for testing the provider load action.
/// Must have a parameterless constructor and implement ISigningProvider.
/// </summary>
public class TestSigningProvider : ISigningProvider
{
    public string Name => "test-signing";
    public bool IsDefault { get; set; }

    public App.Data.@this<KeyPair> GenerateKeyPair()
        => App.Data.@this<KeyPair>.Ok(new KeyPair("testPub", "testPriv"));

    public App.Data.@this Sign(byte[] data, string privateKey)
        => App.Data.@this.Ok(new byte[64]);

    public App.Data.@this Verify(byte[] data, byte[] signature, string publicKey)
        => App.Data.@this.Ok(true);

    public Task<App.Data.@this> SignAsync(sign action)
        => Task.FromResult(App.Data.@this.Ok());

    public Task<App.Data.@this> VerifyAsync(verify action)
        => Task.FromResult(App.Data.@this.Ok(true));
}
