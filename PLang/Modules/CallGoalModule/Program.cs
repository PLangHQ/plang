using PLang.Attributes;
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
	
	[Description("Call a Goal, e.g. `call goal Process`, `call Visits`")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, PrParser prParser, IPLangContextAccessor contextAccessor) : BaseProgram()
	{
		public PLangContext Context { get { return context; } }
		
		[Description("Call/Runs a goal. If backward slash(\\) is used by user, change to forward slash(/)")]
		[Example("call goal Process %name%", @"GoalToCallInfo.Name=Process, GoalToCallInfo.Parameters={""name"":""%name%""}")]
		public async Task<(object? Return, IError? Error)> RunGoal(GoalToCallInfo goalInfo, bool waitForExecution = true,
			int delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, bool keepMemoryStackOnAsync = false, 
			bool isolated = false, bool disableSystemGoals = false)
		{
			try
			{
				string path = (goal != null) ? goal.RelativeAppStartupFolderPath : "/";
				int indent = (goalStep == null) ? 0 : goalStep.Indent;

				var result = await pseudoRuntime.RunGoal(engine, contextAccessor, path, goalInfo, goal,
						waitForExecution, delayWhenNotWaitingInMilliseconds, waitForXMillisecondsBeforeRunningGoal, indent, keepMemoryStackOnAsync, isolated, disableSystemGoals);

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

