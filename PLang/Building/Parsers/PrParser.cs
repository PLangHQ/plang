﻿using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.Text.RegularExpressions;

namespace PLang.Building.Parsers
{
	public class PrParser
	{
		private readonly List<Goal> allGoals = new List<Goal>();
		private readonly List<Goal> publicGoals = new List<Goal>();
		private readonly Dictionary<string, Instruction> instructions = new Dictionary<string, Instruction>();
		private readonly IPLangFileSystem fileSystem;

		public PrParser(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}


		public virtual Goal? ParsePrFile(string absolutePrFilePath)
		{
			var goal = JsonHelper.ParseFilePath<Goal>(fileSystem, absolutePrFilePath);
			if (goal == null)
			{
				return null;
			}
			var appAbsoluteStartupPath = fileSystem.RootDirectory;
			if (!absolutePrFilePath.StartsWith(fileSystem.RootDirectory))
			{
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
			else if (appsPath.StartsWith(Path.DirectorySeparatorChar + ".services" + Path.DirectorySeparatorChar))
			{
				int i = 0;
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

			//var setupOnceDictionary = settings.GetOrDefault<Dictionary<string, DateTime>>(typeof(Engine), "SetupRunOnce", new());
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

				/*if (setupOnceDictionary != null && setupOnceDictionary.ContainsKey(goal.GoalSteps[i].RelativePrPath))
				{
					goal.GoalSteps[i].Executed = setupOnceDictionary[goal.GoalSteps[i].RelativePrPath];
				}*/
				goal.GoalSteps[i].Goal = goal;
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

		public Instruction? ParseInstructionFile(GoalStep step)
		{
			if (!fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				return null;
			}

			if (instructions.TryGetValue(step.AbsolutePrFilePath, out var instruction))
			{
				return instruction;
			}

			instruction = JsonHelper.ParseFilePath<Instruction>(fileSystem, step.AbsolutePrFilePath);
			if (instruction != null) return instruction;

			throw new Exception("Could not parse Instruction file.");
		}
		public List<Goal> ForceLoadAllGoals()
		{
			return LoadAllGoals(true);
		}
		private static readonly object _lock = new object();
		public List<Goal> LoadAllGoals(bool force = false)
		{
			if (allGoals.Count > 0 && !force) return allGoals;

			if (!fileSystem.Directory.Exists(Path.Join(fileSystem.RootDirectory, ".build")))
			{
				return new List<Goal>();
			}
		
			var files = fileSystem.Directory.GetFiles(Path.Join(fileSystem.RootDirectory, ".build"), ISettings.GoalFileName, SearchOption.AllDirectories).ToList();

			files = files.Select(file => new
			{
				FileName = file,
				Order = file.ToLower().EndsWith(@"events\events\00. goal.pr") ? 0 :
					file.ToLower().Contains(@"events\") ? 1 :
					file.ToLower().Contains(@"setup\") ? 2 :
					file.ToLower().Contains(@"start\") ? 3 : 4
			}).OrderBy(file => file.Order)
				.ThenBy(file => file.FileName) 
				.Select(file => file.FileName).ToList();
			if (fileSystem.Directory.Exists(Path.Join(fileSystem.RootDirectory, "apps")))
			{
				var unsortedFiles = fileSystem.Directory.GetFiles(Path.Join(fileSystem.RootDirectory, "apps"), ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
				unsortedFiles = unsortedFiles.Select(file => new
				{
					FileName = file,
					Order = file.ToLower().EndsWith(@"events\events\00. goal.pr") ? 0 :
					file.ToLower().Contains(@"events\") ? 1 :
					file.ToLower().Contains(@"setup\") ? 2 :
					file.ToLower().Contains(@"start\") ? 3 : 4
				})
					.OrderBy(file => file.Order)
					.ThenBy(file => file.FileName) 
					.Select(file => file.FileName).ToList();
				files.AddRange(unsortedFiles);
			}

			var goals = new List<Goal>();
			foreach (var file in files)
			{
				var goal = ParsePrFile(file);
				if (goal != null)
				{
					goals.Add(goal);
				}
			}
			var pubGoals = goals.Where(p => p.Visibility == Visibility.Public).ToList();

			// this reloads the whole app
			lock (_lock)
			{
				allGoals.Clear();
				allGoals.AddRange(goals);
				publicGoals.Clear();
				publicGoals.AddRange(pubGoals);
			}

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
			return ParsePrFile(absolutePrFilePath);
			/*
			if (publicGoals.Count == 0)
			{
				LoadAllGoals();
			}
			return publicGoals.FirstOrDefault(p => p.AbsolutePrFilePath == absolutePrFilePath);
			*/
		}

		public Goal? GetGoalByAppAndGoalName(string appStartupPath, string goalNameOrPath, Goal? callingGoal = null)
		{
			/*
			 * ProcessVideo - goal belonging to same appStartupPath, located in any folder, root first, then by alphabetical order of folders
			 * ui/List - in ui folder, 
			 * apps/HelloWorld - finds a goal located in apps named HelloWorld
			 * apps/Ffmpeg/Convert - maps to apps/Ffmpeg/Convert.goal
			 * if you want to use app, path must start with apps/
			 */
			appStartupPath = appStartupPath.AdjustPathToOs();
			if (appStartupPath == Path.DirectorySeparatorChar.ToString())
			{
				appStartupPath = fileSystem.RootDirectory;
			}
			goalNameOrPath = goalNameOrPath.AdjustPathToOs().Replace(".goal", "").Replace("!", "");

			if (appStartupPath != fileSystem.RootDirectory)
			{
				appStartupPath = appStartupPath.TrimEnd(Path.DirectorySeparatorChar);
				if (!appStartupPath.StartsWith(Path.DirectorySeparatorChar.ToString()))
				{
					appStartupPath = Path.DirectorySeparatorChar.ToString() + appStartupPath;
				}
			}
			Goal? goal = null;
			if (callingGoal != null && !goalNameOrPath.Contains(Path.DirectorySeparatorChar))
			{
				var newGoalPath = Path.Join(callingGoal.RelativePrFolderPath, goalNameOrPath);
				goal = GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath.Equals(newGoalPath, StringComparison.OrdinalIgnoreCase));
				if (goal != null) return goal;
			}

			goal = GetAllGoals().FirstOrDefault(p => p.RelativePrFolderPath.Equals(Path.Join(".build", goalNameOrPath), StringComparison.OrdinalIgnoreCase));
			if (goal != null) return goal;

			// first check for goal inside same goal file as the calling goal
			if (callingGoal != null)
			{
				goal = GetAllGoals().FirstOrDefault(p => p.RelativeGoalFolderPath == callingGoal.RelativeGoalFolderPath && p.GoalName.Equals(goalNameOrPath, StringComparison.OrdinalIgnoreCase));
				if (goal != null) return goal;
			}

			goal = GetAllGoals().FirstOrDefault(p => p.GoalName == goalNameOrPath);
			return goal;
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
