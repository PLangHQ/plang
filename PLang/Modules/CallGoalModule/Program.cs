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
	[Description("Call another Goal, when ! is prefixed, e.g. !RenameFile or !Google/Search.")]
	public class Program(IPseudoRuntime pseudoRuntime, IEngine engine, PrParser prParser, IPLangAppsRepository appsRepository) : BaseProgram()
	{

		[Description("If backward slash(\\) is used by user, change to forward slash(/)")]
		public async Task RunGoal(string goalName, Dictionary<string, object?>? parameters = null, bool waitForExecution = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			if (string.IsNullOrEmpty(goalName))
			{
				throw new RuntimeException($"Could not find goal to call from step: {goalStep.Text}");
			}
			goalName = goalName.Replace("!", "");
			if (goalName.ToLower().Contains("apps/"))
			{
				ValidateAppInstall(goalName);
			}
			
			await pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName,
					variableHelper.LoadVariables(parameters), Goal, 
					waitForExecution, delayWhenNotWaitingInMilliseconds);
			
		}


		private void ValidateAppInstall(string goalToRun)
		{
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

