using app.module.code;
using app.module.signing;
using app.module.signing.code;

namespace NoCtorProvider;

public class NoCtorSigningProvider : ISigning
{
    private readonly string _name;

    public NoCtorSigningProvider(string name) { _name = name; }

    public string Name => _name;
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public (KeyPair? keys, app.error.Error? error) GenerateKeyPair()
        => (new KeyPair("pub", "priv"), null);

    public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey)
        => new global::app.type.item.binary.@this(new byte[64]);

    public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature)
        => new global::app.type.item.@bool.@this(true);

    public Task<app.data.@this> SignAsync(sign action)
        => Task.FromResult(action.Context.Ok());

    public Task<app.data.@this<global::app.type.item.@bool.@this>> VerifyAsync(verify action)
        => Task.FromResult(action.Context.Ok<global::app.type.item.@bool.@this>(true));
}
