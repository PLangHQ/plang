using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json;
using PLang.Interfaces;
using PLang.Services.IdentityService;
using PLang.Utils;
using System.Text;

namespace PLang.Services.SigningService
{
	public interface IPLangSigningService
	{
		Dictionary<string, object> Sign(string content, string method, string url, string contract = "C0");
		Dictionary<string, object> SignWithTimeout(string content, string method, string url, DateTimeOffset expires, string contract = "C0");
		Dictionary<string, object> SignWithTimeout(byte[] seed, string content, string method, string url, DateTimeOffset expires, string contract = "C0");
		Task<string?> VerifySignature(string body, string method, string url, Dictionary<string, object> validationKeyValues);
	}

	public class PLangSigningService : IPLangSigningService
	{
		private readonly IAppCache appCache;
		private readonly IPLangIdentityService identityService;

		public PLangSigningService(IAppCache appCache, IPLangIdentityService identityService)
		{
			this.appCache = appCache;
			this.identityService = identityService;
		}

		public Dictionary<string, object> SignWithTimeout(string content, string method, string url, DateTimeOffset expires, string contract = "C0")
		{
			return SignInternal(content, method, url, contract, expires);
		}
		public Dictionary<string, object> SignWithTimeout(byte[] seed, string content, string method, string url, DateTimeOffset expires, string contract = "C0")
		{
			return SignInternal(seed, content, method, url, contract, expires);
		}
		public Dictionary<string, object> Sign(string content, string method, string url, string contract = "C0")
		{
			return SignInternal(content, method, url, contract);
		}

		private Dictionary<string, object> SignInternal(string content, string method, string url, string contract = "C0", DateTimeOffset? expires = null)
		{
			var identity = identityService.GetCurrentIdentityWithPrivateKey();
			var seed = Encoding.UTF8.GetBytes(identity.Value!.ToString()!);
			return SignInternal(seed, content, method, url, contract, expires);
		}
		private Dictionary<string, object> SignInternal(byte[] seed, string content, string method, string url, string contract = "C0", DateTimeOffset? expires = null)
		{
			// TODO: signing a message should trigger a AskUserException. 
			// this would then ask the user if he want to sign the message
			// the user can accept it and even allow expire date far into future.
			DateTimeOffset created = SystemTime.OffsetUtcNow();
			string nonce = SystemNonce.New();

			var dataToSign = CreateSignatureData(content, method, url, created, nonce, contract, expires);

			var hdWallet = new Nethereum.HdWallet.Wallet(seed);
			dataToSign.Add("X-Signature-Address", hdWallet.GetEthereumKey(0).GetPublicAddress());

			var signer = new EthereumMessageSigner();
			var signature = signer.HashAndSign(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dataToSign)), hdWallet.GetEthereumKey(0));
			dataToSign.Add("X-Signature", signature);
			return dataToSign;


		}

		private static Dictionary<string, object> CreateSignatureData(string body, string method, string url, DateTimeOffset created, string nonce, string contract = "C0", DateTimeOffset? expires = null)
		{

			string hashedBody = body.ComputeHash("keccak256");

			var dict = new Dictionary<string, object>
					{
						{ "X-Signature-Method", method },
						{ "X-Signature-Url", url },
						{ "X-Signature-Created", created.ToUnixTimeMilliseconds() },
						{ "X-Signature-Nonce", nonce },
						{ "X-Signature-Body", hashedBody },
						{ "X-Signature-Contract", contract },
					};

			if (expires != null)
			{
				dict.Add("X-Signature-Expires", expires.Value.ToUnixTimeMilliseconds());
			}
			return dict;

		}

		public async Task<string?> VerifySignature(string body, string method, string url, Dictionary<string, object> validationKeyValues)
		{
			return await VerifySignature(appCache, body, method, url, validationKeyValues);
		}

		/*
		 * Return Identity(string) if signature is valid, else null  
		 */
		public static async Task<string?> VerifySignature(IAppCache appCache, string body, string method, string url, Dictionary<string, object> validationKeyValues)
		{
			var signature = validationKeyValues["X-Signature"];

			if (!long.TryParse(validationKeyValues["X-Signature-Created"].ToString(), out long createdUnixTime))
			{
				throw new SignatureException("X-Signature-Created is invalid. Should be unix time in ms from 1970.");
			}
			var nonce = validationKeyValues["X-Signature-Nonce"];
			var expectedAddress = validationKeyValues["X-Signature-Address"];
			var contract = validationKeyValues["X-Signature-Contract"] ?? "C0";

			DateTimeOffset? expires = null;
			if (validationKeyValues.ContainsKey("X-Signature-Expires"))
			{
				expires = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(validationKeyValues["X-Signature-Expires"].ToString()));
				if (expires < SystemTime.OffsetUtcNow())
				{
					throw new SignatureExpiredException($"Signature expired at {expires}");
				}
			}
			DateTimeOffset signatureCreated = DateTimeOffset.FromUnixTimeMilliseconds(createdUnixTime);
			if (expires == null)
			{
				if (signatureCreated < SystemTime.OffsetUtcNow().AddMinutes(-5))
				{
					throw new SignatureExpiredException("The signature is to old.");
				}

				string nonceKey = "VerifySignature_" + nonce;
				var usedNonce = await appCache.Get(nonceKey);
				if (usedNonce != null)
				{
					throw new SignatureExpiredException("Nonce has been used. New request needs to be created");
				}
				await appCache.Set(nonceKey, true, DateTimeOffset.Now.AddMinutes(5).AddSeconds(5));
			}

			var message = CreateSignatureData(body, method, url, signatureCreated, nonce.ToString(), contract.ToString(), expires);
			message.Add("X-Signature-Address", expectedAddress);

			var signer = new EthereumMessageSigner();
			string recoveredAddress = signer.HashAndEcRecover(JsonConvert.SerializeObject(message), signature.ToString());

			var normalizer = new AddressUtil();
			string recoveredAddress2 = normalizer.ConvertToChecksumAddress(recoveredAddress);
			string expectedAddress2 = normalizer.ConvertToChecksumAddress(expectedAddress.ToString());

			return recoveredAddress2 == expectedAddress2 ? recoveredAddress2 : null;
		}
	}
}
