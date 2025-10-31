using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PLang.Utils
{
	public static class HashHelper
	{

		public static string Hash(object obj)
		{
			// Serialize the object to JSON
			string json = JsonSerializer.Serialize(obj);

			// Convert the JSON string to bytes
			byte[] bytes = Encoding.UTF8.GetBytes(json);

			// Compute the SHA256 hash
			byte[] hashBytes = SHA256.HashData(bytes);

			// Convert hash bytes to hexadecimal string
			return Convert.ToHexString(hashBytes);
		}
	}
}
