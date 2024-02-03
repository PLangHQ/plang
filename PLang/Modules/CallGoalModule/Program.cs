using Org.BouncyCastle.Asn1;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.IO.Compression;
using System.Net;

namespace PLang.Modules.CallGoalModule
{
	[Description("Call another Goal, when ! is prefixed, e.g. !RenameFile or !Google/Search.")]
	public class Program : BaseProgram
	{
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;

		public Program(IPseudoRuntime pseudoRuntime, IEngine engine, IPLangFileSystem fileSystem, PrParser prParser) : base()
		{
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.fileSystem = fileSystem;
			this.prParser = prParser;
		}

		[Description("If backward slash(\\) is used by user, change to forward slash(/)")]
		public async Task RunGoal(string goalName, Dictionary<string, object>? parameters = null, bool waitForExecution = true, int delayWhenNotWaitingInMilliseconds = 0)
		{
			if (goalName == null)
			{
				throw new Exception($"Could not find goal to call from step: {goalStep.Text}");
			}

			if (goalName.Contains("/"))
			{
				ValidateAppInstall(goalName);
			}
			
			await pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalName,
					variableHelper.LoadVariables(parameters), Goal, 
					waitForExecution, delayWhenNotWaitingInMilliseconds);
			
		}

		private void ValidateAppInstall(string goalName)
		{
			var localGoalName = goalName.AdjustPathToOs();
			if (localGoalName.EndsWith(".goal")) localGoalName = localGoalName.Replace(".goal", "");
			localGoalName = Path.Join(".build", localGoalName);

			var goal = prParser.GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath.ToLower() == localGoalName.ToLower());
			if (goal != null) return;

			string appName = goalName.Substring(0, goalName.IndexOf("/"));
			if (!fileSystem.Directory.Exists(Path.Join("apps", appName)))
			{
				string zipPath = Path.Join(fileSystem.RootDirectory, "apps", appName, appName + ".zip");
				using (var client = new HttpClient())
				{
					try
					{
						client.DefaultRequestHeaders.UserAgent.ParseAdd("plang v0.1");
						using (var s = client.GetStreamAsync($"https://raw.githubusercontent.com/PLangHQ/apps/main/{appName}/{appName}.zip"))
						{
							fileSystem.Directory.CreateDirectory(Path.Join("apps", appName));
							using (var fs = new FileStream(zipPath, FileMode.OpenOrCreate))
							{
								s.Result.CopyTo(fs);
							}
						}

						if (fileSystem.File.Exists(zipPath))
						{
							ZipFile.ExtractToDirectory(zipPath, Path.Join(fileSystem.RootDirectory, "apps", appName), true);
							prParser.ForceLoadAllGoals();
						}
						
					}
					catch (Exception ex)
					{
						throw new RuntimeException($"Could not find app {appName} at https://github.com/PLangHQ/apps/. You must put {appName} folder into the apps folder before calling it.");
					}
				}


			}
		}
	}


}

