using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Models;
using PLang.Modules;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Utils.StepHelper;

namespace PLang.Utils
{
	public class CallbackHelper
	{


		public static async Task<(Callback? Callback, Callback? NewCallback, IError? Error)> GetCallback(Modules.IdentityModule.Program identity, string? callbackInfos)
		{
			if (string.IsNullOrEmpty(callbackInfos)) return (null, null, null);

			byte[] bytes = Convert.FromBase64String(callbackInfos);
			string decoded = Encoding.UTF8.GetString(bytes);

			var callback = JsonConvert.DeserializeObject<Callback>(decoded);
			if (callback == null || callback.CallbackInfo == null || callback.Signature == null) return (null, null, null);


			var callbackInfo = callback.CallbackInfo;
			if (callbackInfo == null) return (null, null, new Error("Callback info not valid format", Data: callback.CallbackInfo));

			SignedMessage? signature = callback.Signature;
			if (signature == null) return (null, null, new Error("Signature not valid format", Data: callback.Signature));

			
			// An expiry on the callback comes from a timeout on the ask step. That is a hard
			// limit: the answer is refused and not renewed like the default five minute window.
			if (signature.Expires != null && signature.Expires < SystemTime.OffsetUtcNow())
			{
				return (null, null, new Error("The form has expired", "CallbackExpired", 410));
			}

			var identityKey = await identity.GetMyIdentity();

			if (signature.Identity == null || !signature.Identity.Equals(identityKey.Identifier))
			{
				return (null, null, new Error("Identity does not match"));
			}

			var result = await identity.VerifySignature(signature);
			// A callback is used once. A form that is sent again (a double click, a resend) got a fresh callback and ran its
			// goal a second time without the user seeing anything; it is refused now, with a message the user sees.
			if (result.Error != null && result.Error.Message?.StartsWith(Services.SigningService.PLangSigningService.NonceUsedMessage, StringComparison.OrdinalIgnoreCase) == true)
			{
				return (null, null, new Error("This form has already been sent", "CallbackUsed", 409));
			}
			if (result.Error != null)
			{
				// signature is to old or nonce has been used, create new callback and allow user to sign again
				if (result.Error.StatusCode == 401 || result.Error.StatusCode == 403)
				{
					return (null, await Renew(identity, callback), result.Error);
				}
				
				return (null, null, result.Error);
			}

			if (result.Signature != null)
			{
				return (callback, null, null);
			}
			
			return (null, null, new Error("Signature could not be verified"));

		}

		// The same callback signed again, with a new nonce and the time it had left. The browser finds the form by the
		// old hash and swaps in the new one, so the new callback carries both.
		public static async Task<Callback> Renew(Modules.IdentityModule.Program identity, Callback callback)
		{
			var expires = callback.Signature?.Expires;
			int? secondsLeft = expires != null ? (int)Math.Max(1, (expires.Value - SystemTime.OffsetUtcNow()).TotalSeconds) : null;
			var signed = await identity.Sign(callback, expiresInSeconds: secondsLeft);
			var renewed = new Callback(callback.Path, callback.CallbackData, callback.CallbackInfo, signed);
			renewed.Hash = HashHelper.Hash(renewed);
			renewed.PreviousHash = callback.Hash;
			return renewed;
		}
	}
}
