using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using NBitcoin.Secp256k1;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json;
using NSec.Cryptography;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.SerializerModule;
using PLang.Services.IdentityService;
using PLang.Utils;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
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

		Task<Signature> Sign(object? content, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object>? headers = null);

		Task<(Signature? Signature, IError? Error)> VerifySignature(Dictionary<string, object?> signature);
		Task<(Signature? Signature, IError? Error)> VerifySignature(Signature signature);

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
		/*
		public Dictionary<string, object> SignWithTimeout(string? content, DateTimeOffset expires, string contract = "C0", Dictionary<string, object>? headers = null)
		{
			return SignInternal(content, contract, expires);
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
		*/

		public async Task<Signature> Sign(object? content, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object>? headers = null)
		{

			var identity = identityService.GetCurrentIdentityWithPrivateKey();
			var seed = Convert.FromBase64String(identity.Value!.ToString()!);

			DateTimeOffset? expires = null;
			if (expiresInSeconds > 0)
			{
				expires = DateTimeOffset.Now.AddSeconds(expiresInSeconds.Value);
			}

			var result = await SignInternal(seed, content, headers, contracts, expires);
			var identityObj = new Models.Identity(identity.Value!.ToString()!, identity.Name);
			identityObj.Signature = result;

			result.Identity = identityObj;

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

		private async Task<Signature?> SignInternal(string? content, List<string>? contracts = null, DateTimeOffset? expires = null, Dictionary<string, object?>? headers = null)
		{
			var identity = identityService.GetCurrentIdentityWithPrivateKey();
			var seed = Convert.FromBase64String(identity.Value!.ToString()!);
			var result = await SignInternal(seed, content, headers, contracts, expires);
			identityService.UseSharedIdentity(null);
			return result;
		}
		private Key LoadKeyFromBase64(byte[] privateKeyBytes)
		{
			var algorithm = SignatureAlgorithm.Ed25519;
			return Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
		}

		private async Task<Signature?> SignInternal(byte[] seed, object? content = null, Dictionary<string, object?>? headers = null, List<string>? contracts = null, DateTimeOffset? expires = null)
		{
			// TODO: signing a message should trigger a AskUserException. 
			// this would then ask the user if he want to sign the message
			// the user can accept it and even allow expire date far into future.
			DateTimeOffset created = SystemTime.OffsetUtcNow();
			string nonce = SystemNonce.New();

			var signature = await CreateSignatureData(created, nonce, headers, contracts, expires);

			var key = LoadKeyFromBase64(seed);

			string publicKeyBase64 = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));
			signature.Identity = new PLang.Models.Identity(publicKeyBase64, publicKeyBase64);

			var algorithm = SignatureAlgorithm.Ed25519;
			var bytesToSign = await serialier.Serialize(signature);

			var value = algorithm.Sign(key, bytesToSign);
			signature.SignedData = Convert.ToBase64String(value);
			signature.IsVerified = true;
			return signature;


		}

		private async Task<Signature> CreateSignatureData(DateTimeOffset created, string nonce, Dictionary<string, object?>? headers = null,
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

			if (expires != null)
			{
				signature.ExpiresInMs = expires.Value;
			}

			return signature;
		}

		public async Task<(Signature? Signature, IError? Error)> VerifySignature(Signature signature)
		{
			return await VerifySignature(appCache, signature);
		}

		public async Task<(Signature? Signature, IError? Error)> VerifySignature(Dictionary<string, object> signatureAsDictionary)
		{
			var (signature, error) = SignatureCreator.Cast(signatureAsDictionary);
			if (error != null) return (null, error);

			return await VerifySignature(appCache, signature);
		}


		/*
		 * Return Identity(string) if signature is valid, else null  
		 */
		public async Task<(Signature?, IError?)> VerifySignature(IAppCache appCache, Signature receivedSignature)
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
			DateTimeOffset? expiresInMs = null;

			if (expires < SystemTime.OffsetUtcNow())
			{
				return (null, new ServiceError($"Signature expired at {expires}", GetType(), SignatureInvalid));
			}

			if (receivedSignature.Created < SystemTime.OffsetUtcNow().AddMinutes(-5))
			{
				return (null, new ServiceError("The signature is to old.", GetType(), SignatureInvalid));
			}

			string cacheKey = string.Format(VerifySignatureCacheKey, receivedSignature.Nonce);
			var usedNonce = await appCache.Get(cacheKey);
			if (usedNonce != null)
			{
				return (null, new ServiceError("Nonce has been used. New request needs to be created", GetType(), SignatureInvalid));
			}
			await appCache.Set(cacheKey, true, DateTimeOffset.Now.AddMinutes(5).AddSeconds(5));

			var identifier = expectedIdentity.ToString();
			var name = expectedIdentity?.Name ?? identifier;

			var expectedSignature = await CreateSignatureData(receivedSignature.Created, receivedSignature.Nonce, receivedSignature.Headers, receivedSignature.Contracts, receivedSignature.ExpiresInMs);
			expectedSignature.Identity = null;
			receivedSignature.Identity = null;
			receivedSignature.SignedData = null;

			var (algorithm, publicKey) = GetPublicKey(receivedSignature.Type, expectedIdentity);

			receivedSignature.IsVerified = algorithm.Verify(publicKey, ConvertSignatureToSpan(expectedSignature), ConvertSignatureToSpan(receivedSignature));

			if (receivedSignature.IsVerified)
			{
				receivedSignature.Identity = new Models.Identity(identifier, name);
				context.AddOrReplace(ReservedKeywords.Identity, receivedSignature.Identity);
				return (receivedSignature, null);
			}
			else
			{
				context.AddOrReplace(ReservedKeywords.Identity, null);
				return (null, null);
			}
		}

		private(SignatureAlgorithm algorithm, NSec.Cryptography.PublicKey publicKey) GetPublicKey(string type, Models.Identity expectedIdentity)
		{
			if (type != "Ed25519")
			{
				throw new NotImplementedException("Only Ed25519 is supported");
			}

			var algorithm = SignatureAlgorithm.Ed25519;
			byte[] publicKeyBytes = Convert.FromBase64String(expectedIdentity.ToString());
			var publicKey = NSec.Cryptography.PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
			return (algorithm, publicKey);
		}

		static ReadOnlySpan<byte> ConvertSignatureToSpan(Signature signature)
		{
			byte[] signatureBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(signature));
			return new ReadOnlySpan<byte>(signatureBytes);
		}
	}
}
