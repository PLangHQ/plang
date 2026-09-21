using NJsonSchema.Validation;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Events.Types;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;

namespace PLang.Utils
{
	public class MissingSettingsHelper
	{

		public static async Task<IError?> Handle(IEngine engine, PLangContext context, IEnumerable<IError> missingSettings)
		{
			if (!missingSettings.Any()) return null;

			// todo: this needs to be refactored, it has multiple missingSettings if same key 
			// is requested in same error.
			List<string> asked = new();			
			foreach (var missing in missingSettings)
			{
				if (asked.Contains(missing.Message)) continue;
				asked.Add(missing.Message);

				var error = await HandleMissingSetting(engine, context, (MissingSettingsException)missing.Exception);
				if (error != null) return error;
								
			}
			return null;
		}

		public static async Task<IError?> HandleMissingSetting(IEngine engine, PLangContext context, MissingSettingsException missing)
		{
			(var answer, var error) = await AskUser.GetAnswer(engine, context, missing.Message);
			if (error != null)
			{
				// When asking fails, the failure alone says nothing about what was being asked. Keep
				// the original so the log names the missing setting, not just the broken prompt.
				error.ErrorChain.Add(new Error($"This was asked because a setting is missing: {missing.Message}", Key: "MissingSetting"));
				return error;
			}

			error = await missing.InvokeCallback(answer);
			if (error != null) return error;
			return null;
		}
	}
}
