namespace app.modules.signing.code;

/// <summary>
/// A public/private key pair returned by IKey.
/// </summary>
public record KeyPair(string PublicKey, string PrivateKey);
