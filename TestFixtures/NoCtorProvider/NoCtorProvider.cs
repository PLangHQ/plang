using App.Providers;
using App.modules.signing;
using App.modules.signing.providers;

namespace NoCtorProvider;

public class NoCtorSigningProvider : ISigningProvider
{
    private readonly string _name;

    public NoCtorSigningProvider(string name) { _name = name; }

    public string Name => _name;
    public bool IsDefault { get; set; }

    public App.Data.@this<KeyPair> GenerateKeyPair()
        => App.Data.@this<KeyPair>.Ok(new KeyPair("pub", "priv"));

    public App.Data.@this Sign(byte[] data, string privateKey)
        => App.Data.@this.Ok(new byte[64]);

    public App.Data.@this Verify(byte[] data, byte[] signature, string publicKey)
        => App.Data.@this.Ok(true);

    public Task<App.Data.@this> SignAsync(sign action)
        => Task.FromResult(App.Data.@this.Ok());

    public Task<App.Data.@this> VerifyAsync(verify action)
        => Task.FromResult(App.Data.@this.Ok(true));
}
