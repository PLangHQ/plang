using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Models;
using PLang.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Utils.StepHelper;

namespace PLang.Utils
{
	public class CallbackHelper
	{


		public static async Task<(List<CallbackInfo>? CallbackInfos, IError? Error)> GetCallbackInfos(Modules.IdentityModule.Program identity, string? callbackInfos)
		{
			if (string.IsNullOrEmpty(callbackInfos)) return (null, null);

			var obj = JObject.Parse(callbackInfos);
			if (obj == null || obj["CallbackInfos"] == null || obj["Signature"] == null) return (null, null);


			List<CallbackInfo>? callbacks = obj["CallbackInfos"]?.ToObject<List<CallbackInfo>>();
			if (callbacks == null) return (null, new Error("Callback info not valid format", Data: obj["CallbackInfos"]));

			SignedMessage? signature = obj["Signature"]?.ToObject<SignedMessage>();
			if (signature == null) return (null, new Error("Signature not validate format", Data: obj["Signature"]));

			
			var identityKey = await identity.GetMyIdentity();

			if (signature.Identity == null || !signature.Identity.Equals(identityKey.Identifier))
			{
				return (null, new Error("Identity does not match"));
			}

			var result = await identity.VerifySignature(signature);
			if (result.Error != null) return (null, result.Error);
			if (result.Signature != null)
			{
				return (callbacks, null);
			}
			
			return (null, new Error("Signature could not be verified"));

		}
	}
}
