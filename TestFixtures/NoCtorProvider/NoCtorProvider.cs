using app.Code;
using app.modules.signing;
using app.modules.signing.code;

namespace NoCtorProvider;

public class NoCtorSigningProvider : ISigning
{
    private readonly string _name;

    public NoCtorSigningProvider(string name) { _name = name; }

    public string Name => _name;
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public app.data.@this<KeyPair> GenerateKeyPair()
        => app.data.@this<KeyPair>.Ok(new KeyPair("pub", "priv"));

    public app.data.@this Sign(byte[] data, string privateKey)
        => app.data.@this.Ok(new byte[64]);

    public app.data.@this Verify(byte[] data, byte[] signature, string publicKey)
        => app.data.@this.Ok(true);

    public Task<app.data.@this> SignAsync(sign action)
        => Task.FromResult(app.data.@this.Ok());

    public Task<app.data.@this> VerifyAsync(verify action)
        => Task.FromResult(app.data.@this.Ok(true));
}
