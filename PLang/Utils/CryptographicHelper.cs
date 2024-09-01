using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class CryptographicHelper
	{
		public static string ComputeKeccack(byte[]? bytes)
		{
			if (bytes == null || bytes.Length == 0) return string.Empty;

			var keccak = new Sha3Keccack();

			byte[] hashBytes = keccak.CalculateHash(bytes);

			StringBuilder hashStringBuilder = new StringBuilder();
			foreach (byte b in hashBytes)
			{
				hashStringBuilder.Append(b.ToString("x2"));
			}

			return hashStringBuilder.ToString();
		}
	}
}
