namespace PLang.Interfaces;

public interface IEncryption
{
    void AddPrivateKey(string privateKey);
    T Decrypt<T>(string data, string? keyHash = null);
    string Encrypt(object data, string? keyHash = null);
    void GenerateKey();
    string GetKeyHash();
    string GetPrivateKey(string? keyHash = null);
}