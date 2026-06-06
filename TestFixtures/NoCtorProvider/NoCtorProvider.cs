using app.module.code;
using app.module.signing;
using app.module.signing.code;

namespace NoCtorProvider;

public class NoCtorSigningProvider : ISigning
{
    private readonly string _name;

    public NoCtorSigningProvider(string name) { _name = name; }

    public string Name => _name;
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (KeyPair? keys, app.error.IError? error) GenerateKeyPair()
        => (new KeyPair("pub", "priv"), null);

    public app.data.@this<byte[]> Sign(byte[] data, string privateKey)
        => app.data.@this<byte[]>.Ok(new byte[64]);

    public app.data.@this<global::app.type.@bool.@this> Verify(byte[] data, byte[] signature, string publicKey)
        => app.data.@this<global::app.type.@bool.@this>.Ok(true);

    public Task<app.data.@this<object>> SignAsync(sign action)
        => Task.FromResult(new app.data.@this<object>());

    public Task<app.data.@this<global::app.type.@bool.@this>> VerifyAsync(verify action)
        => Task.FromResult(app.data.@this<global::app.type.@bool.@this>.Ok(true));
}
