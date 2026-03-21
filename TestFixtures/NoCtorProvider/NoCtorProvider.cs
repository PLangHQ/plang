using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace NoCtorProvider;

public class NoCtorSigningProvider : ISigningProvider
{
    private readonly string _name;

    public NoCtorSigningProvider(string name) { _name = name; }

    public string Name => _name;
    public bool IsDefault { get; set; }

    public Data<KeyPair> GenerateKeyPair()
        => Data<KeyPair>.Ok(new KeyPair("pub", "priv"));

    public Data Sign(byte[] data, string privateKey)
        => Data.Ok(new byte[64]);

    public Data Verify(byte[] data, byte[] signature, string publicKey)
        => Data.Ok(true);
}
