using NSec.Cryptography;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Internal Ed25519 key generation via NSec.
/// Returns base64-encoded key pair strings.
/// </summary>
internal static class KeyGenerator
{
    public static (string PublicKey, string PrivateKey) GenerateEd25519()
    {
        var algorithm = SignatureAlgorithm.Ed25519;

        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);

        return (
            Convert.ToBase64String(publicKeyBytes),
            Convert.ToBase64String(privateKeyBytes)
        );
    }
}
