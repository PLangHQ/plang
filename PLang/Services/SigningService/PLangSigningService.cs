using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NSec.Cryptography;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Utilities;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.SerializerModule;
using PLang.Utils;
using Sprache;
using System.Security.Cryptography;
using System.Text;
using static Dapper.SqlMapper;

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

		Task<SignedMessage> Sign(object? data, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object>? headers = null);

		Task<(SignedMessage? Signature, IError? Error)> VerifyDictionarySignature(Dictionary<string, object?> signature, Dictionary<string, object?>? headers = null, object? data = null, List<string>? contracts = null);
		Task<(SignedMessage? Signature, IError? Error)> VerifySignature(SignedMessage signature, Dictionary<string, object?>? headers = null, object? data = null, List<string>? contracts = null);

	}

	public class PLangSigningService : IPLangSigningService
	{
		private readonly IAppCache appCache;
		private readonly IPLangIdentityService identityService;
		private readonly PLangAppContext context;
		private readonly Modules.SerializerModule.Program serialier;
		private readonly Modules.CryptographicModule.Program hasher;
		private readonly string SignatureInvalid = "SignatureInvalid";
		private readonly string VerifySignatureCacheKey = "VerifySignature_{0}";
		public PLangSigningService(IAppCache appCache, IPLangIdentityService identityService, PLangAppContext context,
			Modules.SerializerModule.Program serialier, Modules.CryptographicModule.Program hasher)
		{
			{
				this.appCache = appCache;
				this.identityService = identityService;
				this.context = context;
			}

			this.serialier = serialier;
			this.hasher = hasher;
		}

		public async Task<SignedMessage?> Sign(object? data, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object?>? headers = null)
		{
			var identity = identityService.GetCurrentIdentityWithPrivateKey();
			var seed = Convert.FromBase64String(identity.Value!.ToString()!);

			DateTimeOffset? expires = null;
			if (expiresInSeconds > 0)
			{
				expires = DateTimeOffset.Now.AddSeconds(expiresInSeconds.Value);
			}

			var result = await SignInternal(seed, data, headers, contracts, expires);

			return result;
		}

		public async Task<string> ToBase64(SignedMessage signature)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(signature)));
		}

		public async Task<SignedMessage?> FromBase64(string base64)
		{
			return JsonConvert.DeserializeObject<SignedMessage>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
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

		private async Task<SignedMessage?> SignInternal(byte[] seed, object? data = null, Dictionary<string, object?>? headers = null, List<string>? contracts = null, DateTimeOffset? expires = null)
		{
			// TODO: signing a message should trigger a AskUserException. 
			// this would then ask the user if he want to sign the message
			// the user can accept it and even allow expire date far into future.
			DateTimeOffset created = SystemTime.OffsetUtcNow();
			string nonce = SystemNonce.New();

			var signature = await CreateSignatureData(data, created, nonce, headers, contracts, expires);

			var key = LoadKeyFromBase64(seed);

			string publicKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));
			signature.Identity = publicKeyBase64;

			var bytesToSign = JsonSerializeSignedMessageToBytes(signature);
			

			var algorithm = SignatureAlgorithm.Ed25519;
			var value = algorithm.Sign(key, bytesToSign);
			signature.Signature = Convert.ToBase64String(value);


			return signature;


		}


		private async Task<SignedMessage> CreateSignatureData(object? data, DateTimeOffset created, string nonce, Dictionary<string, object?>? headers = null,
			List<string>? contracts = null, DateTimeOffset? expires = null, string type = "Ed25519")
		{
			var signature = new SignedMessage()
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
			if (data != null)
			{
				var bytes = await serialier.Serialize(data, "json");
				(var hash, var error) = await hasher.Hash(bytes, true, type: "Keccak256");
				signature.Data = new HashedData("Keccak256", "json", hash.ToString());
			}

			if (expires != null)
			{
				signature.Expires = expires.Value;
			}

			return signature;
		}
		/*
		public async Task<(Signature? Signature, IError? Error)> VerifySignature(Signature signature)
		{
			return await VerifySignature(appCache, signature);
		}
		*/

		public async Task<(SignedMessage? Signature, IError? Error)> VerifyDictionarySignature(Dictionary<string, object?>? signatureAsDictionary,
			Dictionary<string, object?>? headers = null, object? data = null, List<string>? contracts = null)
		{
			if (signatureAsDictionary == null)
			{
				return (null, new ServiceError("Signature data is missing", GetType(), SignatureInvalid));
			}

			var (signature, error) = SignatureCreator.Cast(signatureAsDictionary);
			if (error != null) return (null, error);

			return await VerifySignature(signature, headers, data, contracts);
		}


		/*
		 * Return Identity(string) if signature is valid, else null  
		 */
		public async Task<(SignedMessage? Signature, IError? Error)> VerifySignature(SignedMessage clientSignedMessage, Dictionary<string, object?>? headers = null, object? data = null, List<string>? contracts = null)
		{
			if (clientSignedMessage.Created.Ticks == 0)
			{
				return (null, new ServiceError("created is invalid. Should be formatted as yyyy-MM-dd'T'HH:mm:ss.fff'Z'.", GetType(), SignatureInvalid));
			}

			var expectedIdentity = clientSignedMessage.Identity;
			if (expectedIdentity == null)
			{
				return (null, new ServiceError("identity is missing", GetType(), SignatureInvalid));
			}


			var expires = clientSignedMessage.Expires;
			if (expires < SystemTime.OffsetUtcNow())
			{
				return (null, new ServiceError($"Signature expired at {expires}", GetType(), SignatureInvalid));
			}

			if (clientSignedMessage.Created < SystemTime.OffsetUtcNow().AddMinutes(-5))
			{
				return (null, new ServiceError("The signature is to old.", GetType(), SignatureInvalid));
			}

			if (contracts != null)
			{
				if (clientSignedMessage.Contracts.FirstOrDefault(p => contracts.FirstOrDefault(x => x == p) != null) == null)
				{

					return (null, new ServiceError("Contract is invalid", GetType(), SignatureInvalid,
						FixSuggestion: @$"You sent contracts {string.Join(',', clientSignedMessage.Contracts)} but I expected contracts {string.Join(',', contracts)}"));
				}
			}

			string cacheKey = string.Format(VerifySignatureCacheKey, clientSignedMessage.Nonce);
			var usedNonce = await appCache.Get(cacheKey);
			if (usedNonce != null)
			{
				return (null, new ServiceError("Nonce has been used. New request needs to be created", GetType(), SignatureInvalid));
			}
			await appCache.Set(cacheKey, true, DateTimeOffset.Now.AddMinutes(5).AddSeconds(5));
			
			if (clientSignedMessage.Headers != null && clientSignedMessage.Headers.Count > 0)
			{
				if (headers == null || headers.Count == 0) return (null, new ServiceError($"No headers is provided in request", GetType(), "InvalidSignature"));

				foreach (var signedHeader in clientSignedMessage.Headers)
				{
					var item = headers.FirstOrDefault(p => p.Key.Equals(signedHeader.Key, StringComparison.OrdinalIgnoreCase) && p.Value?.Equals(signedHeader.Value) == true);
					if (item.Key == null)
					{
						return (null, new ServiceError($"Header '{signedHeader.Key}' not in received in request or did not match the value", GetType(), "InvalidSignature"));
					}
				}

			}
			if (data != null && clientSignedMessage.Data != null)
			{
				var bytes = await serialier.Serialize(data, clientSignedMessage.Data.Format);
				(var hash, var error) = await hasher.Hash(bytes, true, type: clientSignedMessage.Data.Type);

				string? value = clientSignedMessage.Data?.Hash.ToString();
				if (value == null || !value.Equals(hash))
				{
					return (null, new ServiceError($"Data does not match in value to received signature", GetType(), "InvalidSignatureData"));
				}
			}


			var signature = clientSignedMessage.Signature;
			clientSignedMessage.Signature = null;

			bool isValid = false;

			if (clientSignedMessage.Type.Equals("Ed25519", StringComparison.OrdinalIgnoreCase))
			{
				var (algorithm, publicKey) = GetPublicKey(clientSignedMessage.Type, clientSignedMessage.Identity.ToString());

				isValid = algorithm.Verify(publicKey, ConvertSignedMessageToSpan(clientSignedMessage), ConvertSignatureToSpan(signature));
			} else if (clientSignedMessage is SignedMessageJwkIdentity signedMessageJwkIdentity)
			{ 
				try
				{
					var ecdsaSignedMessage = signedMessageJwkIdentity with { Identity = null, Signature = null, JwkIdentity = null  };
					
					string x_b64 = signedMessageJwkIdentity.JwkIdentity["x"].ToString();
					string y_b64 = signedMessageJwkIdentity.JwkIdentity["y"].ToString();

					byte[] X = Base64UrlDecode(x_b64);
					byte[] Y = Base64UrlDecode(y_b64);

					var ecParams = new ECParameters
					{
						Curve = ECCurve.NamedCurves.nistP256,
						Q = new ECPoint { X = X, Y = Y }
					};

					using var ecdsa = ECDsa.Create(ecParams);
					

					var signedDataSpan = ConvertSignedMessageToSpan(ecdsaSignedMessage);
					var signatureSpan  = ConvertSignatureToSpan(signature);


					isValid = ecdsa.VerifyData(signedDataSpan, signatureSpan, HashAlgorithmName.SHA256);
				} catch (Exception ex)
				{
					return (null, new ExceptionError(ex));
				}
			} else
			{
				return (null, new ServiceError($"The type {clientSignedMessage.Type} for signatures are not supported", GetType(), "InvalidSignature"));
			}

			if (isValid)
			{
				clientSignedMessage.Signature = signature;
				return (clientSignedMessage, null);
			}

			return (null, new ServiceError("Signature is invalid", GetType(), "InvalidSignature"));

		}

		static byte[] Base64UrlDecode(string base64Url)
		{
			string s = base64Url.Replace('-', '+').Replace('_', '/');
			switch (s.Length % 4)
			{
				case 2: s += "=="; break;
				case 3: s += "="; break;
			}
			return Convert.FromBase64String(s);
		}

		private (SignatureAlgorithm algorithm, NSec.Cryptography.PublicKey publicKey) GetPublicKey(string type, string identifier)
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
		private ReadOnlySpan<byte> ConvertSignatureToSpan(string data)
		{
			byte[] signatureBytes = Convert.FromBase64String(data);
			return new ReadOnlySpan<byte>(signatureBytes);
		}

		private ReadOnlySpan<byte> ConvertSignedMessageToSpan(SignedMessage signature)
		{
			byte[] signatureBytes = JsonSerializeSignedMessageToBytes(signature);
			return new ReadOnlySpan<byte>(signatureBytes);
		}



		private byte[] JsonSerializeSignedMessageToBytes(SignedMessage signature)
		{
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Include,
				DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			};
			string json = JsonConvert.SerializeObject(signature, settings);

			
			var bytes = Encoding.UTF8.GetBytes(json);

			string base64 = Convert.ToBase64String(bytes);
			string hex = BytesToHex(bytes);
			return bytes;
		}

		string BytesToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

	}
}
