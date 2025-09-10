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

			
			var identityKey = await identity.GetMyIdentity();

			if (signature.Identity == null || !signature.Identity.Equals(identityKey.Identifier))
			{
				return (null, null, new Error("Identity does not match"));
			}

			var result = await identity.VerifySignature(signature);
			if (result.Error != null)
			{
				// signature is to old or nonce has been used, create new callback and allow user to sign again
				if (result.Error.StatusCode == 401 || result.Error.StatusCode == 403)
				{
					var signed = await identity.Sign(callback);
					return (null, new Callback(callback.Path, callback.CallbackData, callback.CallbackInfo, signed), result.Error);
				}
				
				return (null, null, result.Error);
			}

			if (result.Signature != null)
			{
				return (callback, null, null);
			}
			
			return (null, null, new Error("Signature could not be verified"));

		}
	}
}
