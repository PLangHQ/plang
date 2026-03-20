using NSec.Cryptography;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Ed25519 signing provider using NSec. Handles key generation, signing, and verification.
/// </summary>
public class Ed25519Provider : ISigningProvider
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }

    public KeyPair GenerateKeyPair()
    {
        var algorithm = SignatureAlgorithm.Ed25519;

        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);

        return new KeyPair(
            Convert.ToBase64String(publicKeyBytes),
            Convert.ToBase64String(privateKeyBytes)
        );
    }

    public byte[] Sign(byte[] data, string privateKeyBase64)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

        using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        return algorithm.Sign(key, data);
    }

    public bool Verify(byte[] data, byte[] signature, string publicKeyBase64)
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

        var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return algorithm.Verify(publicKey, data, signature);
    }
}
