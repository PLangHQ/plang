using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using NBitcoin.Secp256k1;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json;
using NSec.Cryptography;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.IdentityService;
using PLang.Utils;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static Dapper.SqlMapper;
using Identity = PLang.Interfaces.Identity;

namespace PLang.Services.SigningService
{
	public class SignatureExpiredException : Exception
	{
		public SignatureExpiredException(string message) : base(message) { }
	}
	public class SignatureException : Exception
	{
		public SignatureException(string message) : base(message) { }
	}

	public interface IPLangSigningService
	{
		Task<string> GetPublicKey();
		Dictionary<string, object> Sign(byte[] seed, string content, string method, string url, string contract = "C0");
		Dictionary<string, object> Sign(string? content, string method, string url, string contract = "C0", string? appId = null);
		Dictionary<string, object> SignWithTimeout(byte[] seed, string content, string method, string url, DateTimeOffset expires, string contract = "C0");
		Dictionary<string, object> SignWithTimeout(string content, string method, string url, DateTimeOffset expires, string contract = "C0", string? appId = null);
		Task<Dictionary<string, object?>> VerifySignature(string body, string method, string url, Dictionary<string, object> validationKeyValues);
	}

	public class PLangSigningService : IPLangSigningService
	{
		private readonly IAppCache appCache;
		private readonly IPLangIdentityService identityService;
		private readonly PLangAppContext context;

		public PLangSigningService(IAppCache appCache, IPLangIdentityService identityService, PLangAppContext context)
		{
			this.appCache = appCache;
			this.identityService = identityService;
			this.context = context;
		}

		public Dictionary<string, object> SignWithTimeout(string? content, string method, string url, DateTimeOffset expires, string contract = "C0", string? appId = null)
		{
			return SignInternal(content, method, url, contract, expires, appId);
		}
		public Dictionary<string, object> SignWithTimeout(byte[] seed, string content, string method, string url, DateTimeOffset expires, string contract = "C0")
		{
			return SignInternal(seed, content, method, url, contract, expires);
		}
		public Dictionary<string, object> Sign(string? content, string method, string url, string contract = "C0", string? appId = null)
		{
			return SignInternal(content, method, url, contract, null, appId);
		}
		public Dictionary<string, object> Sign(byte[] seed, string content, string method, string url, string contract = "C0")
		{
			return SignInternal(seed, content, method, url, contract, null);
		}

		public async Task<string> GetPublicKey()
		{
			var identity = identityService.GetCurrentIdentity();
			return identity.Value!.ToString()!;
		}

		private Dictionary<string, object> SignInternal(string? content, string method, string url, string contract = "C0", DateTimeOffset? expires = null, string? appId = null)
		{
			identityService.UseSharedIdentity(appId);
			var identity = identityService.GetCurrentIdentityWithPrivateKey();

			var seed = Convert.FromBase64String(identity.Value!.ToString()!);
			var result = SignInternal(seed, content, method, url, contract, expires);
			identityService.UseSharedIdentity(null);
			return result;
		}
		private Key LoadKeyFromBase64(byte[] privateKeyBytes)
		{
			var algorithm = SignatureAlgorithm.Ed25519;
			return Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
		}

		private Dictionary<string, object> SignInternal(byte[] seed, string? content, string method, string url, string contract = "C0", DateTimeOffset? expires = null)
		{
			// TODO: signing a message should trigger a AskUserException. 
			// this would then ask the user if he want to sign the message
			// the user can accept it and even allow expire date far into future.
			DateTimeOffset created = SystemTime.OffsetUtcNow();
			string nonce = SystemNonce.New();

			var dataToSign = CreateSignatureData(content, method, url, created, nonce, contract, expires);

			var key = LoadKeyFromBase64(seed);

			string publicKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));
			dataToSign.Add("X-Signature-Public-Key", publicKeyBase64);

			var algorithm = SignatureAlgorithm.Ed25519;
			var signature = algorithm.Sign(key, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dataToSign)));
			dataToSign.Add("X-Signature", Convert.ToBase64String(signature));
			return dataToSign;


		}

		private static Dictionary<string, object> CreateSignatureData(string? body, string method, string url, DateTimeOffset created, string nonce, string contract = "C0", DateTimeOffset? expires = null)
		{
			if (body == null) body = "";

			string hashedBody = body.ComputeHash().Hash;

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


		public async Task<Dictionary<string, object?>> VerifySignature(string body, string method, string url, Dictionary<string, object> validationKeyValues)
		{
			return await VerifySignature(appCache, body, method, url, validationKeyValues);
		}

		/*
		 * Return Identity(string) if signature is valid, else null  
		 */
		public static async Task<Dictionary<string, object?>> VerifySignature(IAppCache appCache, string body, string method, string url, Dictionary<string, object> validationKeyValues)
		{
			var identities = new Dictionary<string, object?>();
			if (validationKeyValues.ContainsKey("X-Signature-Address"))
			{
				throw new SignatureException("You must update you plang runtime. Visit https://github.com/PLangHQ/plang/releases to get latest version.");
			}
			
			if (!validationKeyValues.ContainsKey("X-Signature"))
			{
				identities.AddOrReplace(ReservedKeywords.Identity, null);
				return identities;
			}
			var signature = validationKeyValues["X-Signature"];

			if (!long.TryParse(validationKeyValues["X-Signature-Created"].ToString(), out long createdUnixTime))
			{
				throw new SignatureException("X-Signature-Created is invalid. Should be unix time in ms from 1970.");
			}
			var nonce = validationKeyValues["X-Signature-Nonce"];
			var expectedPublicKey = validationKeyValues["X-Signature-Public-Key"] as string;
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
			message.Add("X-Signature-Public-Key", expectedPublicKey);

			var algorithm = SignatureAlgorithm.Ed25519;
			byte[] publicKeyBytes = Convert.FromBase64String(expectedPublicKey);
			var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);


			var isVerified = algorithm.Verify(publicKey, ConvertDictionaryToSpan(message), ConvertSignatureToSpan(signature.ToString()));

			if (isVerified)
			{
				identities.AddOrReplace(ReservedKeywords.Identity, expectedPublicKey);
				return identities;
			}

			identities.AddOrReplace(ReservedKeywords.Identity, null);

			return identities;
		}
		static ReadOnlySpan<byte> ConvertDictionaryToSpan(Dictionary<string, object?> data)
		{
			// Serialize dictionary to JSON using Newtonsoft.Json
			string jsonString = JsonConvert.SerializeObject(data);
			byte[] jsonData = Encoding.UTF8.GetBytes(jsonString);
			return new ReadOnlySpan<byte>(jsonData);
		}

		static ReadOnlySpan<byte> ConvertSignatureToSpan(string signatureBase64)
		{
			byte[] signatureBytes = Convert.FromBase64String(signatureBase64);
			return new ReadOnlySpan<byte>(signatureBytes);
		}
	}
}
