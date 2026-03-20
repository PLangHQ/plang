namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// A public/private key pair returned by IKeyProvider.
/// </summary>
public record KeyPair(string PublicKey, string PrivateKey);
