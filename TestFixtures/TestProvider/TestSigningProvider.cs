using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.signing;
using PLang.Runtime2.modules.signing.providers;

namespace TestProvider;

/// <summary>
/// A minimal ISigningProvider for testing the provider load action.
/// Must have a parameterless constructor and implement ISigningProvider.
/// </summary>
public class TestSigningProvider : ISigningProvider
{
    public string Name => "test-signing";
    public bool IsDefault { get; set; }

    public Data<KeyPair> GenerateKeyPair()
        => Data<KeyPair>.Ok(new KeyPair("testPub", "testPriv"));

    public Data Sign(byte[] data, string privateKey)
        => Data.Ok(new byte[64]);

    public Data Verify(byte[] data, byte[] signature, string publicKey)
        => Data.Ok(true);

    public Task<Data> SignAsync(sign action)
        => Task.FromResult(Data.Ok());

    public Task<Data> VerifyAsync(verify action)
        => Task.FromResult(Data.Ok(true));
}
