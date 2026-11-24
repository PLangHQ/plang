using Newtonsoft.Json;
using PLang.Errors.Runtime;
using PLang.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLang.Models;
using PLang.Modules.IdentityModule;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace PLang.Utils
{
	public class SignatureCreator
	{

		public static readonly string InvalidSignature = "InvalidSignature";
		
		public static (SignedMessage? Signature, IError? Error) Cast(Dictionary<string, object?>? dict)
		{
			if (dict == null)
			{
				return (null, new ServiceError("Signature is null", typeof(SignedMessage), Key: InvalidSignature));
			}

			var signature = new SignedMessage();
			signature.Type = GetByKey("type", dict).ToString();

			var createdResult = GetCreated(dict);
			if (createdResult.Error != null)
			{
				return (null, createdResult.Error);
			}

			signature.Created = (DateTimeOffset)createdResult.Created;

			if (dict.ContainsKey("headers"))
			{
				signature.Headers = (Dictionary<string, object?>)dict["headers"];
			}

			var identity = GetByKey("identity", dict);
			signature.Identity = identity.ToString();

			var result = GetExpiresInMs(dict);
			if (result.Error != null)
			{
				return (null, result.Error);
			}

			signature.Expires = result.Expires;
			signature.Nonce = dict["nonce"].ToString();
			if (dict.ContainsKey("parent"))
			{
				var parentSignatureResult = Cast(dict["parent"] as Dictionary<string, object?>);
				if (parentSignatureResult.Error != null)
				{
					return (null, parentSignatureResult.Error);
				}

				signature.Parent = parentSignatureResult.Signature;
			}
			/*
			if (signature.Identity == null)
			{
				return (null, new ServiceError("You must update you plang runtime. Visit https://github.com/PLangHQ/plang/releases to get latest version.", typeof(Signature), "IdentityNotAvailable"));
			}*/


			return (signature, null);
		}
		public static (DateTimeOffset? Expires, IError? Error) GetExpiresInMs(Dictionary<string, object?> dict)
		{
			if (!dict.ContainsKey("expires")) return (null, null);
			if (dict["expires"] is long lngCreated)
			{
				return (DateTimeOffset.FromUnixTimeMilliseconds(lngCreated), null);
			}
			return (null, new ServiceError("Signature expires is not long and could not be converted: '" + dict["expires"], typeof(SignedMessage), Key: InvalidSignature));
		}


		public static (DateTimeOffset? Created, IError? Error) GetCreated(Dictionary<string, object?> dict)
		{
			if (!dict.ContainsKey("created"))
			{
				return (null, new ServiceError("Signature created date not found", typeof(SignedMessage), Key: InvalidSignature));
			}

			if (dict["created"] is long lngCreated)
			{
				return (DateTimeOffset.FromUnixTimeMilliseconds(lngCreated), null);
			}

			return (null, new ServiceError("Signature created date not long: '" + dict["created"], typeof(SignedMessage), Key: InvalidSignature));
		}

		public static object? GetByKey(string key, Dictionary<string, object?> dict)
		{
			var item = dict.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			return item.Value;
		}

		/// <summary>
		/// A signature as json text. One made in a browser carries its public key as a P-256 jwk (jwkIdentity) and its
		/// identity is the hash of that key. Returns null when the text is not a signature or the jwk is not a key.
		/// </summary>
		internal static SignedMessage? Parse(string str)
		{
			JObject jobj;
			try { jobj = JObject.Parse(str); }
			catch (JsonException) { return null; }

			var jwk = jobj["jwkIdentity"];
			if (jwk != null && jwk.Type != JTokenType.Null)
			{
				if (jwk is not JObject jwkObject
					|| jwkObject["x"] is not JValue { Type: JTokenType.String } xToken
					|| jwkObject["y"] is not JValue { Type: JTokenType.String } yToken)
				{
					return null;
				}

				var signature = JsonConvert.DeserializeObject<SignedMessageJwkIdentity>(str);
				if (signature == null) return null;

				string x = xToken.ToString();
				string y = yToken.ToString();
				string keyString = $"{{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"{x}\",\"y\":\"{y}\"}}";
				using var sha = SHA256.Create();
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(keyString));
				signature.Identity = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

				return signature;
			} else
			{
				return JsonConvert.DeserializeObject<SignedMessage>(str);
			}
		}
	}
}
