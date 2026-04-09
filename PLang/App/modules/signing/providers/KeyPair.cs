namespace App.modules.signing.providers;

/// <summary>
/// A public/private key pair returned by IKeyProvider.
/// </summary>
public record KeyPair(string PublicKey, string PrivateKey);
