using Org.BouncyCastle.Asn1;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.AppsRepository;
using PLang.Utils;
using System.ComponentModel;
using System.IO.Compression;
using System.Net;

namespace PLang.Modules.CallGoalModule
{
	[Description("Call another Goal, when ! is prefixed, example: call !RenameFile, call app !Google/Search, call !ui/ShowItems, call goal !DoStuff")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, IPLangAppsRepository appsRepository, PrParser prParser) : BaseProgram()
	{

		[Description("If backward slash(\\) is used by user, change to forward slash(/)")]
		public async Task RunGoal(string goalName, Dictionary<string, object?>? parameters = null, bool waitForExecution = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			if (string.IsNullOrEmpty(goalName))
			{
				throw new RuntimeException($"Goal name is missing from step: {goalStep.Text}");
			}

			ValidateAppInstall(goalName);

			await pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName,
					variableHelper.LoadVariables(parameters), Goal, 
					waitForExecution, delayWhenNotWaitingInMilliseconds);
			
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

