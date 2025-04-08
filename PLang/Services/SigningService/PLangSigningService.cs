using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSec.Cryptography;
using Org.BouncyCastle.Asn1.Cms;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.SerializerModule;
using PLang.Utils;
using System.Text;

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

		Task<Signature> Sign(object? body, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object>? headers = null);

		Task<(Signature? Signature, IError? Error)> VerifyDictionarySignature(Dictionary<string, object?> signature, Dictionary<string, object?>? headers = null, object? body = null, List<string>? contracts = null);
		Task<(Signature? Signature, IError? Error)> VerifySignature(Signature signature, Dictionary<string, object?>? headers = null, object? body = null, List<string>? contracts = null);

	}

	public class PLangSigningService : IPLangSigningService
	{
		private readonly IAppCache appCache;
		private readonly IPLangIdentityService identityService;
		private readonly PLangAppContext context;
		private readonly Program serialier;
		private readonly Modules.CryptographicModule.Program hasher;
		private readonly string SignatureInvalid = "SignatureInvalid";
		private readonly string VerifySignatureCacheKey = "VerifySignature_{0}";
		public PLangSigningService(IAppCache appCache, IPLangIdentityService identityService, PLangAppContext context, Modules.SerializerModule.Program serialier, Modules.CryptographicModule.Program hasher)
		{
			{
				this.appCache = appCache;
				this.identityService = identityService;
				this.context = context;
			}

			this.serialier = serialier;
			this.hasher = hasher;
		}

		public async Task<Signature?> Sign(object? body, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object?>? headers = null)
		{
			var identity = identityService.GetCurrentIdentityWithPrivateKey();
			var seed = Convert.FromBase64String(identity.Value!.ToString()!);

			DateTimeOffset? expires = null;
			if (expiresInSeconds > 0)
			{
				expires = DateTimeOffset.Now.AddSeconds(expiresInSeconds.Value);
			}

			var result = await SignInternal(seed, body, headers, contracts, expires);
			
			return result;
		}

		public async Task<string> ToBase64(Signature signature)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(signature)));
		}

		public async Task<Signature?> FromBase64(string base64)
		{
			return JsonConvert.DeserializeObject<Signature>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
		}


		public async Task<string> GetPublicKey()
		{
			var identity = identityService.GetCurrentIdentity();
			return identity.Value!.ToString()!;
		}

		private Key LoadKeyFromBase64(byte[] privateKeyBytes)
		{
			var algorithm = SignatureAlgorithm.Ed25519;
			return Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
		}

		private async Task<Signature?> SignInternal(byte[] seed, object? body = null, Dictionary<string, object?>? headers = null, List<string>? contracts = null, DateTimeOffset? expires = null)
		{
			// TODO: signing a message should trigger a AskUserException. 
			// this would then ask the user if he want to sign the message
			// the user can accept it and even allow expire date far into future.
			DateTimeOffset created = SystemTime.OffsetUtcNow();
			string nonce = SystemNonce.New();

			var signature = await CreateSignatureData(body, created, nonce, headers, contracts, expires);

			var key = LoadKeyFromBase64(seed);

			string publicKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));
			signature.Identity = publicKeyBase64;

			var algorithm = SignatureAlgorithm.Ed25519;
			var bytesToSign = await serialier.Serialize(signature);

			var value = algorithm.Sign(key, bytesToSign);
			signature.SignedData = Convert.ToBase64String(value);
			return signature;


		}

		private async Task<Signature> CreateSignatureData(object? body, DateTimeOffset created, string nonce, Dictionary<string, object?>? headers = null,
			List<string>? contracts = null, DateTimeOffset? expires = null, string type = "Ed25519")
		{
			var signature = new Signature()
			{
				Type = type,
				Created = created,
				Nonce = nonce,
				Contracts = contracts ?? ["C0"]
			};
			if (headers != null && headers.Count > 0)
			{
				signature.Headers = headers;
			}
			if (body != null)
			{
				signature.Body = JsonConvert.SerializeObject(body);
			}

			if (expires != null)
			{
				signature.ExpiresInMs = expires.Value;
			}

			return signature;
		}
		/*
		public async Task<(Signature? Signature, IError? Error)> VerifySignature(Signature signature)
		{
			return await VerifySignature(appCache, signature);
		}
		*/

		public async Task<(Signature? Signature, IError? Error)> VerifyDictionarySignature(Dictionary<string, object?>? signatureAsDictionary, 
			Dictionary<string, object?>? headers = null, object? body = null, List<string>? contracts = null)
		{
			if (signatureAsDictionary == null)
			{
				return (null, new ServiceError("Signature data is missing", GetType(), SignatureInvalid));
			}

			var (signature, error) = SignatureCreator.Cast(signatureAsDictionary);
			if (error != null) return (null, error);

			return await VerifySignature(signature, headers, body, contracts);
		}


		/*
		 * Return Identity(string) if signature is valid, else null  
		 */
		public async Task<(Signature? Signature, IError? Error)> VerifySignature(Signature receivedSignature, Dictionary<string, object?>? headers = null, object? body = null, List<string>? contracts = null)
		{

			if (receivedSignature.Created.Ticks == 0)
			{
				return (null, new ServiceError("created is invalid. Should be unix time in ms from 1970.", GetType(), SignatureInvalid));
			}

			var expectedIdentity = receivedSignature.Identity;
			if (expectedIdentity == null)
			{
				return (null, new ServiceError("identity is missing", GetType(), SignatureInvalid));
			}


			var expires = receivedSignature.ExpiresInMs;
			if (expires < SystemTime.OffsetUtcNow())
			{
				return (null, new ServiceError($"Signature expired at {expires}", GetType(), SignatureInvalid));
			}

			if (receivedSignature.Created < SystemTime.OffsetUtcNow().AddMinutes(-5))
			{
				return (null, new ServiceError("The signature is to old.", GetType(), SignatureInvalid));
			}

			if (contracts != null)
			{

				if (receivedSignature.Contracts.FirstOrDefault(p => contracts.FirstOrDefault(x => x == p) != null) == null)
				{

					return (null, new ServiceError("Contract is invalid", GetType(), SignatureInvalid,
						FixSuggestion: @$"You sent contracts {string.Join(',', receivedSignature.Contracts)} but I expected contracts {string.Join(',', contracts)}"));
				} else
				{
					receivedSignature.Contracts = contracts;
				}
			}

			string cacheKey = string.Format(VerifySignatureCacheKey, receivedSignature.Nonce);
			var usedNonce = await appCache.Get(cacheKey);
			if (usedNonce != null)
			{
				return (null, new ServiceError("Nonce has been used. New request needs to be created", GetType(), SignatureInvalid));
			}
			await appCache.Set(cacheKey, true, DateTimeOffset.Now.AddMinutes(5).AddSeconds(5));

			receivedSignature.Headers = headers;
			receivedSignature.Body = (body != null && body is not JValue) ? JsonConvert.SerializeObject(body) : body?.ToString();
			

			var signedData = receivedSignature.SignedData;
			receivedSignature.SignedData = null;
			Console.WriteLine(JsonConvert.SerializeObject(receivedSignature));
			var (algorithm, publicKey) = GetPublicKey(receivedSignature.Type, receivedSignature.Identity.ToString());

			bool isValid = algorithm.Verify(publicKey, ConvertSignatureObjectToSpan(receivedSignature), ConvertSignedDataToSpan(signedData));

			if (isValid)
			{
				receivedSignature.SignedData = signedData;
				context.AddOrReplace(ReservedKeywords.Identity, receivedSignature.Identity);
				return (receivedSignature, null);
			}
			else
			{
				context.AddOrReplace(ReservedKeywords.Identity, null);
				return (null, null);
			}
		}

		private(SignatureAlgorithm algorithm, NSec.Cryptography.PublicKey publicKey) GetPublicKey(string type, string identifier)
		{
			if (type != "Ed25519")
			{
				throw new NotImplementedException("Only Ed25519 is supported");
			}

			var algorithm = SignatureAlgorithm.Ed25519;
			byte[] publicKeyBytes = Convert.FromBase64String(identifier);
			var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
			return (algorithm, publicKey);
		}
		static ReadOnlySpan<byte> ConvertSignedDataToSpan(string data)
		{
			byte[] signatureBytes = Convert.FromBase64String(data);
			return new ReadOnlySpan<byte>(signatureBytes);
		}
		static ReadOnlySpan<byte> ConvertSignatureObjectToSpan(Signature signature)
		{
			byte[] signatureBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(signature));
			return new ReadOnlySpan<byte>(signatureBytes);
		}
	}
}
