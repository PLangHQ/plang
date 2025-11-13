using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Utils;
using System.ComponentModel;

namespace PLang.Modules.AiModule
{

	public record AiInfo(string model, object config);
	
	[Description("Call an App. When the user has the word 'app' in his statement, this should be called. ")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, PrParser prParser, IPLangContextAccessor contextAccessor) : BaseProgram()
	{
		public PLangContext Context { get { return context; } }

		[Description("Call/Runs another app. app can be located in another directory, then path points the way. goalName is default \"Start\" when it cannot be mapped")]
		public async Task<(object? Variables, IError? Error)> RunAi(AiInfo aiInfo)
		{
			var goalToCall = new GoalToCallInfo("")
			{

			};

			return await engine.RunGoal(goalToCall, goal, context);
			/*
			IEngine newEngine = await engine.GetEnginePool(goal.AbsoluteAppStartupFolderPath).RentAsync(engine, goalStep, appToCall.AppName + "_" + appToCall.Name);
			var newContext = new PLangContext(memoryStack.Clone(newEngine), newEngine, ExecutionMode.Console);
			try
			{
				if (appToCall.Parameters != null)
				{
					foreach (var item in appToCall.Parameters)
					{
						if (item.Key.StartsWith("!"))
						{
							newContext.AddOrReplace(item.Key, this.memoryStack.LoadVariables(item.Value));
						}
						else
						{
							newContext.MemoryStack.Put(item.Key, item.Value, goalStep: goalStep);
						}
					}
				}


				(var vars, error) = await newEngine.RunGoal(goal, context);

				return (vars, error);

			}
			catch (Exception ex)
			{
				throw;
			}
			finally
			{
				engine.GetEnginePool(goal.AbsoluteAppStartupFolderPath).Return(newEngine);

			}*/
		}




	}


}

