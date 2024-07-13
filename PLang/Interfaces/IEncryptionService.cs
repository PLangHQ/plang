namespace PLang.Interfaces
{
	public interface IEncryption
	{
		T Decrypt<T>(string data);
		string Encrypt(object data);
		void GenerateKey();
		string GetKeyHash();
		string GetPrivateKey();
	}
}
