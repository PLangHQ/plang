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

    public app.Data.@this<KeyPair> GenerateKeyPair()
        => app.Data.@this<KeyPair>.Ok(new KeyPair("pub", "priv"));

    public app.Data.@this Sign(byte[] data, string privateKey)
        => app.Data.@this.Ok(new byte[64]);

    public app.Data.@this Verify(byte[] data, byte[] signature, string publicKey)
        => app.Data.@this.Ok(true);

    public Task<app.Data.@this> SignAsync(sign action)
        => Task.FromResult(app.Data.@this.Ok());

    public Task<app.Data.@this> VerifyAsync(verify action)
        => Task.FromResult(app.Data.@this.Ok(true));
}
