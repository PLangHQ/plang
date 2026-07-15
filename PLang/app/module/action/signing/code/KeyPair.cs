namespace app.module.action.signing.code;

/// <summary>
/// A public/private key pair returned by IKey. A PLang value (: item).
/// </summary>
public sealed class KeyPair
{
    public string PublicKey { get; }
    public string PrivateKey { get; }
    public KeyPair(string PublicKey, string PrivateKey)
    {
        this.PublicKey = PublicKey;
        this.PrivateKey = PrivateKey;
    }
}
