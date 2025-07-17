using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Errors.Handlers.HttpErrorHandler;

namespace PLang.Utils
{
	public class StepHelper
	{

		public static ErrorHandler? GetErrorHandlerForStep(List<ErrorHandler>? errorHandlers, IError? error)
		{
			if (errorHandlers == null) return null;
			if (error == null) return null;

			foreach (var errorHandler in errorHandlers)
			{
				if (string.IsNullOrEmpty(errorHandler.Message) &&
						string.IsNullOrEmpty(errorHandler.Key) &&
						errorHandler.StatusCode == null)
				{
					return errorHandler;
				}

				if (!string.IsNullOrEmpty(errorHandler.Message) && error.Message.Contains(errorHandler.Message, StringComparison.OrdinalIgnoreCase))
				{
					return errorHandler;
				}

				if (!string.IsNullOrEmpty(errorHandler.Key) && (errorHandler.Key == "*" || error.Key.Equals(errorHandler.Key, StringComparison.OrdinalIgnoreCase)))
				{
					return errorHandler;
				}

				if (errorHandler.StatusCode != null && error.StatusCode == errorHandler.StatusCode)
				{
					return errorHandler;
				}
			}
			return null;
		}

		public record Callback(string Path, Dictionary<string, object?>? CallbackData, Stack<CallbackInfo> CallbackInfos, SignedMessage Signature);
		public record CallbackInfo(string GoalName, string GoalHash, int StepIndex, string? Answer = null);
		public static async Task<Callback?> GetCallback(string path, Dictionary<string, object?>? callbackData, 
			Runtime.MemoryStack memoryStack, GoalStep? step, Modules.ProgramFactory programFactory, bool skipNonce = false)
		{
			if (step == null) return null;

			
			var callbackInfos = new Stack<CallbackInfo>();
			var goal = step.Goal;
			string method = goal.GoalName;

			callbackInfos.Push(new CallbackInfo(goal.GoalName, goal.Hash, goal.CurrentStepIndex));

			if (goal.ParentGoal != null)
			{
				var parentGoal = goal.ParentGoal;
				while (parentGoal != null)
				{
					callbackInfos.Push(new CallbackInfo(parentGoal.GoalName, parentGoal.Hash, parentGoal.CurrentStepIndex));
					parentGoal = parentGoal.ParentGoal;
				}
			}
			var encryption = programFactory.GetProgram<Modules.CryptographicModule.Program>(step);

			if (callbackData != null)
			{
				foreach (var item in callbackData)
				{
					var obj = memoryStack.Get(item.Key);
					if (obj != null)
					{
						var encryptedValue = await encryption.Encrypt(obj);
						callbackData.AddOrReplace(item.Key, encryptedValue);
					}
				}
			}
			var signed = await programFactory.GetProgram<Modules.IdentityModule.Program>(step).Sign(callbackInfos, skipNonce : skipNonce);
			return new Callback(path, callbackData, callbackInfos, signed);
		}
	}
}
