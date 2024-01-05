using NBitcoin.Secp256k1;
using PLang.Interfaces;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class Signature
	{
		private readonly ISettings settings;
		private readonly PLangAppContext context;

		public Signature(ISettings settings, PLangAppContext context)
		{
			this.settings = settings;
			this.context = context;
		}

		public Dictionary<string, string> Sign(string data, string method, string url, string contract)
		{
			var dict = new Dictionary<string, string>();
			DateTime created = SystemTime.UtcNow();
			string nonce = Guid.NewGuid().ToString();
			string dataToSign = StringHelper.CreateSignatureData(method, url, created.ToFileTimeUtc(), nonce, data, contract);

			var p = new Modules.BlockchainModule.Program(settings, context, null, null, null, null, null);
			string signedMessage = p.SignMessage(dataToSign).Result;
			string address = p.GetCurrentAddress().Result;

			dict.Add("X-Signature", signedMessage);
			dict.Add("X-Signature-Contract", contract);
			dict.Add("X-Signature-Created", created.ToFileTimeUtc().ToString());
			dict.Add("X-Signature-Nonce", nonce);
			dict.Add("X-Signature-Address", address);
			return dict;
		}

		public string VerifySignature(string body, string method, string url, Dictionary<string, string> validationHeaders)
		{
			var signature = validationHeaders["X-Signature"];
			var created = validationHeaders["X-Signature-Created"];
			var nonce = validationHeaders["X-Signature-Nonce"];
			var address = validationHeaders["X-Signature-Address"];
			var contract = validationHeaders["X-Signature-Contract"] ?? "C0";

			DateTime signatureCreated = DateTime.FromFileTime(long.Parse(created));
			if (signatureCreated < DateTime.UtcNow.AddMinutes(-5))
			{
				throw new Exception("The signature is to old.");
			}

			string message = StringHelper.CreateSignatureData(method, url, long.Parse(created), nonce, body, contract);
			var p = new Modules.BlockchainModule.Program(settings, context, null, null, null, null, null);
			if (p.VerifySignature(message, signature, address).Result)
			{
				return address;
			}
			return null;
		}
	}
}
