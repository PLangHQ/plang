using NBitcoin.Protocol;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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

		public record Callback(string Path, Dictionary<string, object?>? CallbackData, CallbackInfo CallbackInfo, SignedMessage Signature) { 
			public string Hash { get; set; }
			public string? PreviousHash { get; set; }
		}
		public record CallbackInfo(string GoalName, string GoalHash, int StepIndex, string? Answer = null);
		public static async Task<Callback?> GetCallback(string path, Dictionary<string, object?>? callbackData, 
			Runtime.MemoryStack memoryStack, GoalStep? step, Modules.ProgramFactory programFactory, bool skipNonce = false)
		{
			if (step == null) return null;

			
	//		var callbackInfos = new Stack<CallbackInfo>();
			var goal = step.Goal;
			string method = goal.GoalName;

			var callBackInfo = new CallbackInfo(goal.GoalName, goal.Hash, goal.CurrentStepIndex);
			//			callbackInfos.Push(new CallbackInfo(goal.GoalName, goal.Hash, goal.CurrentStepIndex));

			/*
			 * TODO: fix this
			 * 
			 * List<string> callStackGoals is a temp fix, the ParentGoal should not be set on goal object
			 * it should be set on CallStack object that needs to be created, goal object should
			 * not change at runtime. this is because if same goal is called 2 or more times
			 * in a callstack, the parent goal is overwritten
			 * 
			List<string> callStackGoals = new();
			callStackGoals.Add(goal.RelativePrPath);


			var parentGoal = goal.ParentGoal;
			while (parentGoal != null)
			{					
				if (callStackGoals.Contains(parentGoal.RelativePrPath))
				{
					parentGoal = null;
				} else
				{
					callbackInfos.Push(new CallbackInfo(parentGoal.GoalName, parentGoal.Hash, parentGoal.CurrentStepIndex));
					callStackGoals.Add(parentGoal.RelativePrPath);

					parentGoal = parentGoal.ParentGoal;
					
				}

				// todo: temp thing while figuring out to deep calls
				int counter = 0;
				if (counter++ > 100)
				{
					Console.WriteLine($"To deep: GoalHelper.IsPartOfCallStack - goalName: {goal?.GoalName}");
					break;
				}

				
			}

			*/

			var encryption = programFactory.GetProgram<Modules.CryptographicModule.Program>(step);

			if (callbackData != null)
			{
				foreach (var item in callbackData)
				{
					if (item.Value == null) continue;

					if (VariableHelper.IsVariable(item.Value))
					{
						var obj = memoryStack.Get(item.Value.ToString());
						if (obj != null)
						{
							var encryptedValue = await encryption.Encrypt(obj);
							callbackData.AddOrReplace(item.Key, encryptedValue);
						}
					}
					else
					{
						var encryptedValue = await encryption.Encrypt(item.Value);
						callbackData.AddOrReplace(item.Key, encryptedValue);
					}
				}
			}
			var signed = await programFactory.GetProgram<Modules.IdentityModule.Program>(step).Sign(callBackInfo, skipNonce : skipNonce);
			var callBack = new Callback(path, callbackData, callBackInfo, signed);
			var hash = HashHelper.Hash(callBack);
			callBack.Hash = hash;
			return callBack;
		}
	}
}
