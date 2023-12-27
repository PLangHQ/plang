using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Utils;
using System.Security.Cryptography;
using System.Text;

namespace PLang.Services.EncryptionService
{
	public class Encryption : IEncryption
	{
		private readonly ISettings settings;
		
		public Encryption(ISettings settings)
		{
			this.settings = settings;
		}

		public record EncryptionKey(string PrivateKey, bool IsDefault = false, bool IsArchived = false);
		public void GenerateKey()
		{
			var keys = settings.GetValues<EncryptionKey>(this.GetType());
			if (keys != null && keys.Count > 0) return;

			using (var aes = Aes.Create())
			{
				aes.GenerateKey();

				keys.Add(new EncryptionKey(Convert.ToBase64String(aes.Key), (keys.Count == 0)));

				settings.SetList(this.GetType(), keys);
			}

		}

		public string GetKeyHash()
		{
			var key = GetKey();
			return key.PrivateKey.ComputeHash();
		}

		private EncryptionKey GetKey()
		{
			var keys = settings.GetValues<EncryptionKey>(this.GetType());
			if (keys.Count == 0)
			{
				GenerateKey();
				keys = settings.GetValues<EncryptionKey>(this.GetType());
			}
			var key = keys.FirstOrDefault(p => p.IsDefault);
			if (key != null) return key;

			return keys[0];
		}

		public string Encrypt(object data)
		{
			var json = JsonConvert.SerializeObject(data);
			byte[] dataBytes = Encoding.UTF8.GetBytes(json);
			var key = GetKey();
			using (var aes = Aes.Create())
			{
				aes.Key = Convert.FromBase64String(key.PrivateKey);
				aes.GenerateIV(); // Generate a new Initialization Vector (IV) each time for better security

				byte[] encryptedData;
				using (var encryptor = aes.CreateEncryptor())
				{
					encryptedData = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
				}

				// Combine IV and encrypted data for decryption later
				byte[] result = new byte[aes.IV.Length + encryptedData.Length];
				Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
				Buffer.BlockCopy(encryptedData, 0, result, aes.IV.Length, encryptedData.Length);

				return Convert.ToBase64String(result);
			}
		}


		public T Decrypt<T>(string data)
		{
			byte[] allBytes = Convert.FromBase64String(data);

			// Extract the IV and encrypted data
			byte[] iv = new byte[16]; // AES IV is always 16 bytes regardless of the key size
			Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
			byte[] encryptedData = new byte[allBytes.Length - iv.Length];
			Buffer.BlockCopy(allBytes, iv.Length, encryptedData, 0, encryptedData.Length);
			var key = GetKey();
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
				return JsonConvert.DeserializeObject<T>(decryptedJson);
			}


		}
	}
}
