using app.modules.code;
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

    public app.data.@this<byte[]> Sign(byte[] data, string privateKey)
        => app.data.@this<byte[]>.Ok(new byte[64]);

    public app.data.@this<bool> Verify(byte[] data, byte[] signature, string publicKey)
        => app.data.@this<bool>.Ok(true);

    public Task<app.data.@this<object>> SignAsync(sign action)
        => Task.FromResult(new app.data.@this<object>());

    public Task<app.data.@this<bool>> VerifyAsync(verify action)
        => Task.FromResult(app.data.@this<bool>.Ok(true));
}
