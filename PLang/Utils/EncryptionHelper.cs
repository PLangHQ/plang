using System.Security.Cryptography;

namespace PLang.Utils
{
	public class EncryptionHelper
	{
		private static string[] supportedAlgos = ["SHA56", "SHA512", "SHA1", "MD5"];

		public static HashAlgorithm GetCryptoStandard(string algorithm, string expectedHash)
		{
			int idx = expectedHash.IndexOf("-");
			if (idx != -1)
			{
				var algo = expectedHash.Substring(0, idx);
				var supported = supportedAlgos.FirstOrDefault(p => p.Equals(algo, StringComparison.OrdinalIgnoreCase));
				if (supported != null)
				{
					algorithm = supported;
				}
			}
			switch (algorithm.ToUpperInvariant())
			{
				case "SHA256":
					return SHA256.Create();
				case "SHA512":
					return SHA512.Create();
				case "SHA1":
					return SHA1.Create();
				case "MD5":
					return MD5.Create();
				default:
					throw new NotSupportedException($"Algorithm {algorithm} is not supported.");
			}
		}
	}
}
