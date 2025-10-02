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

namespace PLang.Modules.CallGoalModule
{
	
	[Description("Call another Goal or App, when ! is prefixed, example: call !RenameFile, call app !Google/Search, call !ui/ShowItems, call goal !DoStuff, set %formatted% = Format(%data%), %user% = GetUser %id%")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, PrParser prParser, IPLangContextAccessor contextAccessor) : BaseProgram()
	{
		public PLangContext Context { get { return context; } }

		[Description("Call/Runs another app. app can be located in another directory, then path points the way. goalName is default \"Start\" when it cannot be mapped")]
		public async Task<(object? Variables, IError? Error)> RunApp(AppToCallInfo appToCall, bool waitForExecution = true,
			int delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, bool keepMemoryStackOnAsync = false)
		{
			
			var (goals, error) = await prParser.LoadAppPath(appToCall.AppName, fileAccessHandler);
			if (error != null) return (null, error);

			var goal = goals!.FirstOrDefault(p => p.GoalName == appToCall.Name);

			if (goal == null)
			{
				return (null, new ProgramError($"Could not find goal {appToCall.Name} in {appToCall.AppName}", goalStep, function));
			}
			
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

			}
		}


		[Description("Call/Runs another goal. goalName can be prefixed with !. If backward slash(\\) is used by user, change to forward slash(/)")]
		public async Task<(object? Return, IError? Error)> RunGoal(GoalToCallInfo goalInfo, bool waitForExecution = true,
			int delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, bool keepMemoryStackOnAsync = false, 
			bool isolated = false, bool disableSystemGoals = false, bool isEvent = false)
		{
			try
			{
				string path = (goal != null) ? goal.RelativeAppStartupFolderPath : "/";
				int indent = (goalStep == null) ? 0 : goalStep.Indent;

				var result = await pseudoRuntime.RunGoal(engine, contextAccessor, path, goalInfo, goal,
						waitForExecution, delayWhenNotWaitingInMilliseconds, waitForXMillisecondsBeforeRunningGoal, indent, keepMemoryStackOnAsync, isolated, disableSystemGoals, isEvent);

				if (result.Error is Return ret)
				{
					return (ret.ReturnVariables, null);
				}

				/*
				if (result.error is EndGoal endGoal && (goal == null || GoalHelper.IsPartOfCallStack(goal, endGoal)) && endGoal.Levels == 0)
				{
					return (result.Variables, null);
				}*/

				return (result.Variables, result.Error);
			} catch (Exception ex)
			{
				Console.WriteLine("RunGoal:" + ex.ToString());
				throw;
			}

		}

		/*
		private void ValidateAppInstall(string goalToRun)
		{
			if (string.IsNullOrEmpty(goalToRun)) return;
			if (!goalToRun.Contains("apps")) return;

			goalToRun = goalToRun.AdjustPathToOs().Replace("!", "");

			string appName = GoalHelper.GetAppName(goalToRun);
			string goalName = GoalHelper.GetGoalPath(goalToRun);

			string buildPath = fileSystem.Path.Join(fileSystem.Path.DirectorySeparatorChar.ToString(), "apps", appName, ".build", goalName);
			var goal = prParser.GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath.ToLower() == buildPath.ToLower());
			if (goal != null) return;

			appsRepository.InstallApp(appName);


		}*/


	}


}

