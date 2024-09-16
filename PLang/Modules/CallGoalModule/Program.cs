using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.AppsRepository;
using PLang.Utils;
using System.ComponentModel;

namespace PLang.Modules.CallGoalModule
{
	[Description("Call another Goal, when ! is prefixed, example: call !RenameFile, call app !Google/Search, call !ui/ShowItems, call goal !DoStuff")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, IPLangAppsRepository appsRepository, PrParser prParser) : BaseProgram()
	{

		[Description("If backward slash(\\) is used by user, change to forward slash(/)")]
		public async Task<IError?> RunGoal(GoalToCall goalName, Dictionary<string, object?>? parameters = null, bool waitForExecution = true, 
			int delayWhenNotWaitingInMilliseconds = 50, uint waitForXMillisecondsBeforeRunningGoal = 0, bool keepMemoryStackOnAsync = false)
		{
			if (string.IsNullOrEmpty(goalName))
			{
				throw new RuntimeException($"Goal name is missing from step: {goalStep.Text}");
			}

			ValidateAppInstall(goalName);
			

			var result = await pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName,
					variableHelper.LoadVariables(parameters), Goal, 
					waitForExecution, delayWhenNotWaitingInMilliseconds, waitForXMillisecondsBeforeRunningGoal, goalStep.Indent, keepMemoryStackOnAsync);
			return result.error;
			
		}


		private void ValidateAppInstall(string goalToRun)
		{
			if (string.IsNullOrEmpty(goalToRun)) return;
			if (!goalToRun.Contains("apps")) return;

			goalToRun = goalToRun.AdjustPathToOs().Replace("!", "");

			string appName = GoalHelper.GetAppName(goalToRun);
			string goalName = GoalHelper.GetGoalPath(goalToRun);

			string buildPath = Path.Join(Path.DirectorySeparatorChar.ToString(), "apps", appName, ".build", goalName);
			var goal = prParser.GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath.ToLower() == buildPath.ToLower());
			if (goal != null) return;

			appsRepository.InstallApp(appName);


		}


	}


}

