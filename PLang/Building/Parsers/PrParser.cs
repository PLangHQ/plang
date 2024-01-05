using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Building.Parsers
{
	public class PrParser
	{
		private readonly List<Goal> allGoals = new List<Goal>();
		private readonly List<Goal> publicGoals = new List<Goal>();
		private readonly Dictionary<string, Instruction> instructions = new Dictionary<string, Instruction>();
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;

		public PrParser(IPLangFileSystem fileSystem, ISettings settings)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
		}


		public virtual Goal? ParsePrFile(string absolutePrFilePath)
		{
			var goal = JsonHelper.ParseFilePath<Goal>(fileSystem, absolutePrFilePath);
			if (goal == null)
			{
				return null;
			}
			var appAbsoluteStartupPath = fileSystem.RootDirectory;
			if (!absolutePrFilePath.StartsWith(fileSystem.RootDirectory)) {
				appAbsoluteStartupPath = absolutePrFilePath.Substring(0, absolutePrFilePath.IndexOf(".build"));
			}

			var appsPath = absolutePrFilePath.Replace(appAbsoluteStartupPath, "");
			if (appsPath.StartsWith(Path.DirectorySeparatorChar + "apps" + Path.DirectorySeparatorChar))
			{
				var paths = appsPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
				appsPath = Path.DirectorySeparatorChar + paths[0] + Path.DirectorySeparatorChar + paths[1];
				goal.AppName = paths[1];

				goal.RelativeAppStartupFolderPath = appsPath;
				goal.RelativeGoalFolderPath = Path.TrimEndingDirectorySeparator(Path.Join(appsPath, goal.RelativeGoalFolderPath));
				goal.RelativeGoalPath = Path.TrimEndingDirectorySeparator(Path.Join(appsPath, goal.RelativeGoalPath));
				goal.RelativePrPath = Path.TrimEndingDirectorySeparator(Path.Join(appsPath, goal.RelativePrPath));
				goal.RelativePrFolderPath = Path.TrimEndingDirectorySeparator(Path.Join(appsPath, goal.RelativePrFolderPath));
				goal.AbsoluteAppStartupFolderPath = Path.TrimEndingDirectorySeparator(Path.Join(appAbsoluteStartupPath, appsPath));

			}
			else
			{
				goal.AppName = Path.DirectorySeparatorChar.ToString();

				goal.AbsoluteAppStartupFolderPath = appAbsoluteStartupPath;
				goal.RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString();
			}


			goal.AbsoluteGoalPath = Path.Join(appAbsoluteStartupPath, goal.RelativeGoalPath);
			goal.AbsoluteGoalFolderPath = Path.Join(appAbsoluteStartupPath, goal.RelativeGoalFolderPath);

			goal.AbsolutePrFilePath = Path.Join(appAbsoluteStartupPath, goal.RelativePrPath);
			goal.AbsolutePrFolderPath = Path.Join(appAbsoluteStartupPath, goal.RelativePrFolderPath);

			AdjustPathsToOS(goal);

			var dict = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
			for (int i = 0; i < goal.GoalSteps.Count; i++)
			{
				goal.GoalSteps[i].AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, goal.GoalSteps[i].PrFileName).AdjustPathToOs();
				goal.GoalSteps[i].RelativePrPath = Path.Join(goal.RelativePrFolderPath, goal.GoalSteps[i].PrFileName).AdjustPathToOs();
				goal.GoalSteps[i].AppStartupPath = appAbsoluteStartupPath.AdjustPathToOs();
				goal.GoalSteps[i].Number = i;
				if (goal.GoalSteps.Count > i + 1)
				{
					goal.GoalSteps[i].NextStep = goal.GoalSteps[i + 1];
				}

				if (dict != null && dict.ContainsKey(goal.GoalSteps[i].RelativePrPath))
				{
					goal.GoalSteps[i].Executed = dict[goal.GoalSteps[i].RelativePrPath];
				}
			}
			return goal;
		}

		private static void AdjustPathsToOS(Goal goal)
		{
			goal.RelativeAppStartupFolderPath = goal.RelativeAppStartupFolderPath.AdjustPathToOs();
			goal.RelativeGoalFolderPath = goal.RelativeGoalFolderPath.AdjustPathToOs();
			goal.RelativeGoalPath = goal.RelativeGoalPath.AdjustPathToOs();
			goal.RelativePrPath = goal.RelativePrPath.AdjustPathToOs();
			goal.RelativePrFolderPath = goal.RelativePrFolderPath.AdjustPathToOs();

			goal.AbsoluteAppStartupFolderPath = goal.AbsoluteAppStartupFolderPath.AdjustPathToOs();
			goal.AbsoluteGoalPath = goal.AbsoluteGoalPath.AdjustPathToOs();
			goal.AbsoluteGoalFolderPath = goal.AbsoluteGoalFolderPath.AdjustPathToOs();
			goal.AbsolutePrFilePath = goal.AbsolutePrFilePath.AdjustPathToOs();
			goal.AbsolutePrFolderPath = goal.AbsolutePrFolderPath.AdjustPathToOs();
		}

		public Instruction ParseInstructionFile(GoalStep step)
		{
			if (instructions.TryGetValue(step.AbsolutePrFilePath, out var instruction))
			{
				return instruction;
			}

			instruction = JsonHelper.ParseFilePath<Instruction>(fileSystem, step.AbsolutePrFilePath);
			return instruction;
		}
		public List<Goal> ForceLoadAllGoals()
		{
			allGoals.Clear();
			return LoadAllGoals();
		}

		public List<Goal> LoadAllGoals()
		{
			if (allGoals.Count > 0) return allGoals;

			if (!fileSystem.Directory.Exists(fileSystem.RootDirectory))
			{
				return new List<Goal>();
			}

			string[] files = fileSystem.Directory.GetFiles(fileSystem.RootDirectory, ISettings.GoalFileName, SearchOption.AllDirectories);
			allGoals.Clear();
			foreach (var file in files)
			{
				var goal = ParsePrFile(file);
				if (goal != null)
				{
					for (int i=0;i<goal.GoalSteps.Count;i++)
					{
						goal.GoalSteps[i].Goal = goal;
					}
					allGoals.Add(goal);
				}
			}
			publicGoals.Clear();
			publicGoals.AddRange(allGoals.Where(p => p.Visibility == Visibility.Public).ToList());
			return allGoals;
		}

		public List<Goal> GetAllGoals()
		{
			if (allGoals.Count > 0) return allGoals;

			LoadAllGoals();
			return allGoals;
		}

		public List<Goal> GetPublicGoals()
		{
			if (publicGoals.Count > 0) return publicGoals;
			LoadAllGoals();
			return publicGoals;
		}

		public Goal? GetGoal(string absolutePrFilePath)
		{
			if (publicGoals.Count == 0)
			{
				LoadAllGoals();
			}
			return publicGoals.FirstOrDefault(p => p.AbsolutePrFilePath == absolutePrFilePath);
		}

		public Goal? GetGoalByAppAndGoalName(string appStartupPath, string goalName, Goal? callingGoal = null)
		{
			if (Path.IsPathRooted(goalName) && Path.GetExtension(goalName) == ".pr")
			{
				return ParsePrFile(goalName);
			}

			(goalName, appStartupPath) = GetGoalAndPath(appStartupPath, goalName);
			Goal? goal;
			if (callingGoal != null)
			{
				var goalPath = Path.DirectorySeparatorChar + Path.GetRelativePath(callingGoal.AbsoluteAppStartupFolderPath, goalName.Replace(".goal", "")).ToLower() + ".goal";
				goal = allGoals.FirstOrDefault(p => p.RelativeAppStartupFolderPath == appStartupPath && (p.GoalName == goalName || p.RelativeGoalPath.ToLower() == goalPath));
				if (goal != null) return goal;
			}
			else if (appStartupPath == Settings.GlobalPath)
			{
				var goalPath = Path.Combine(appStartupPath, goalName);
				goal = ParsePrFile(goalPath);
				if (goal != null) return goal;
			}

			if (goalName.Contains("\\") || goalName.Contains("/"))
			{
				var formatted = goalName.Replace("\\", "/").Replace("/", Path.DirectorySeparatorChar.ToString());
				var path = formatted.Substring(0, formatted.LastIndexOf(Path.DirectorySeparatorChar.ToString()));

				goalName = formatted.Substring(formatted.LastIndexOf(Path.DirectorySeparatorChar.ToString()) + 1).ToLower();
				var buildPath = Path.Join(".build", path, goalName).ToLower();

				goal = allGoals.FirstOrDefault(p => p.RelativeAppStartupFolderPath == appStartupPath && p.RelativePrFolderPath.ToLower() == buildPath && p.GoalName.ToLower() == goalName);
				if (goal != null) return goal;
			}

			return allGoals.FirstOrDefault(p => p.RelativeAppStartupFolderPath == appStartupPath && p.GoalName == goalName);
		
		}

		private (string, string) GetGoalAndPath(string appStartupPath, string goalName)
		{
			
			goalName = goalName.Replace("!", "").Replace(".goal", "");
			string path = appStartupPath;
			// if goalName has . then it's referencing an app
			if (goalName.Contains("."))
			{
				var paths = goalName.Split('.').ToList();
				paths.Insert(0, appStartupPath);
				paths.Insert(1, "apps");
				paths.RemoveAt(paths.Count - 1);
				goalName = paths[paths.Count - 1];
				path = Path.Join(paths.ToArray());
			}
			else if (appStartupPath == Settings.GlobalPath)
			{
				path = Settings.GlobalPath;
			}
			else
			{
				path = Path.DirectorySeparatorChar.ToString();

			}

			return (goalName, path);
		}

		internal List<Goal> GetGoalsAvailable(string appPath, string goalName)
		{
			(goalName, appPath) = GetGoalAndPath(appPath, goalName);
			return allGoals.Where(p => p.RelativeAppStartupFolderPath == appPath).ToList();
		}

		public List<Goal> GetApps()
		{
			var groupedGoals = GetAllGoals().GroupBy(p => p.AppName);
			var goals = new List<Goal>();
			foreach (var groupedGoal in groupedGoals)
			{
				var goal = groupedGoal.FirstOrDefault();
				if (goal != null && goal.RelativeAppStartupFolderPath.StartsWith(Path.DirectorySeparatorChar + "apps"))
				{
					goals.Add(goal);
				}
			}
			return goals;
		}

	}
}
