using NSec.Cryptography;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Ed25519 signing provider using NSec. Handles key generation, signing, and verification.
/// </summary>
public class Ed25519Provider : ISigningProvider
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }

    public Data<KeyPair> GenerateKeyPair()
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;

            using var key = Key.Create(algorithm, new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            });

            var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
            var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);

            return Data<KeyPair>.Ok(new KeyPair(
                Convert.ToBase64String(publicKeyBytes),
                Convert.ToBase64String(privateKeyBytes)
            ));
        }
        catch (Exception ex)
        {
            return Data<KeyPair>.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
        }
    }

    public Data Sign(byte[] data, string privateKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

            using var key = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            var signature = algorithm.Sign(key, data);
            return Data.Ok(signature);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "SigningError", 500));
        }
    }

    public Data Verify(byte[] data, byte[] signature, string publicKeyBase64)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

            var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var isValid = algorithm.Verify(publicKey, data, signature);

            if (!isValid)
                return Data.FromError(new ActionError("Signature verification failed", "SignatureInvalid", 400));

            return Data.Ok(true);
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "SignatureInvalid", 400));
        }
    }
}
