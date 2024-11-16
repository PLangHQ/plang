using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.EncryptionService;

//todo: should cleanup any usage of keys, set them to null after usage
// this is so they dont stay in memory until garbage collection
// this needs to happen down the call stack and figure out how settings is handled
public class Encryption : IEncryption
{
    private readonly ISettings settings;

    public Encryption(ISettings settings)
    {
        this.settings = settings;
    }

    public void GenerateKey()
    {
        var keys = settings.GetValues<EncryptionKey>(GetType());
        if (keys.Count > 0) return;

        using (var aes = Aes.Create())
        {
            aes.GenerateKey();

            keys.Add(new EncryptionKey(Convert.ToBase64String(aes.Key), keys.Count == 0));

            settings.SetList(GetType(), keys);
        }
    }

    public void AddPrivateKey(string privateKey)
    {
        var keys = settings.GetValues<EncryptionKey>(GetType());
        if (keys.FirstOrDefault(p => p.PrivateKey == privateKey) == null)
        {
            keys.Add(new EncryptionKey(privateKey, keys.Count == 0));
            settings.SetList(GetType(), keys);
        }
    }

    public string GetKeyHash()
    {
        var key = GetKey();
        return key.PrivateKey.ComputeHash().Hash;
    }

    public string Encrypt(object data, string? keyHash = null)
    {
        var json = JsonConvert.SerializeObject(data);
        var dataBytes = Encoding.UTF8.GetBytes(json);
        var key = GetKey(keyHash);
        using (var aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key.PrivateKey);
            aes.GenerateIV();

            byte[] encryptedData;
            using (var encryptor = aes.CreateEncryptor())
            {
                encryptedData = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
            }

            // Combine IV and encrypted data for decryption later
            var result = new byte[aes.IV.Length + encryptedData.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedData, 0, result, aes.IV.Length, encryptedData.Length);
            key = null;

            return Convert.ToBase64String(result);
        }
    }


    public T Decrypt<T>(string data, string? keyHash = null)
    {
        var allBytes = Convert.FromBase64String(data);

        // Extract the IV and encrypted data
        var iv = new byte[16]; // AES IV is always 16 bytes regardless of the key size
        Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
        var encryptedData = new byte[allBytes.Length - iv.Length];
        Buffer.BlockCopy(allBytes, iv.Length, encryptedData, 0, encryptedData.Length);
        var key = GetKey(keyHash);
        using (var aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key.PrivateKey);
            aes.IV = iv;

            byte[] decryptedData;
            using (var decryptor = aes.CreateDecryptor())
            {
                decryptedData = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            }

            var decryptedJson = Encoding.UTF8.GetString(decryptedData);
            var result = JsonConvert.DeserializeObject<T>(decryptedJson);
            if (result != null) return result;

            throw new Exception("Could not decrypt data");
        }
    }

    public string GetPrivateKey(string? keyHash = null)
    {
        var key = GetKey(keyHash);
        return key.PrivateKey;
    }

    private EncryptionKey GetKey(string? keyHash = null)
    {
        var keys = settings.GetValues<EncryptionKey>(GetType());
        if (keys.Count == 0)
        {
            GenerateKey();
            keys = settings.GetValues<EncryptionKey>(GetType());
        }

        EncryptionKey? key;
        if (keyHash != null)
            key = keys.FirstOrDefault(p => p.PrivateKey.ComputeHash().Hash == keyHash);
        else
            key = keys.FirstOrDefault(p => p.IsDefault);
        if (key != null) return key;

        throw new Exception("Key to decrypt could not be found");
    }

    public record EncryptionKey(string PrivateKey, bool IsDefault = false, bool IsArchived = false);
}