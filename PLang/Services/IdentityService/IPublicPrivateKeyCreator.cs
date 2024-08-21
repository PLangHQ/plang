using NBitcoin;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.IdentityService
{
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
			using var key = NSec.Cryptography.Key.Create(algorithm, new KeyCreationParameters
			{
				ExportPolicy = KeyExportPolicies.AllowPlaintextExport
			});

			string privateKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey));

			// Convert the public key to Base64 string
			string publicKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));


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
}
