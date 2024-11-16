using NSec.Cryptography;

namespace PLang.Services.IdentityService;

public interface IPublicPrivateKeyCreator
{
    public PublicPrivateKey Create();
}

public class PublicPrivateKeyCreator : IPublicPrivateKeyCreator
{
    public PublicPrivateKey Create()
    {
        // select the Ed25519 signature algorithm
        var algorithm = SignatureAlgorithm.Ed25519;

        // create a new key pair
        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        var privateKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey));

        // Convert the public key to Base64 string
        var publicKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));


        var pk = new PublicPrivateKey(publicKeyBase64, privateKeyBase64);
        return pk;
    }
}

public class PublicPrivateKey(string publicKey, string privateKey) : IDisposable
{
    public void Dispose()
    {
        //	publicKey = string.Empty;
        //privateKey = string.Empty;
    }

    public string GetPublicKey()
    {
        return publicKey;
    }

    public string GetPrivateKey()
    {
        return privateKey;
    }
}