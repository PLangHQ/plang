namespace PLang.Interfaces
{
	public interface IEncryption
	{
		void AddPrivateKey(string privateKey);
		T Decrypt<T>(string data, string? keyHash = null);
		T Decrypt<T>(byte[] bytes, string? keyHash = null);
		string Encrypt(object data, string? keyHash = null);
		byte[] Encrypt(byte[] bytes, string? keyHash = null);
		void GenerateKey();
		string GetKeyHash();
		string GetPrivateKey(string? keyHash = null);
	}
}
