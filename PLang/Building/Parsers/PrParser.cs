﻿using Microsoft.IdentityModel.Tokens;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using ReverseMarkdown.Converters;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Export;
using static PLang.Runtime.Startup.ModuleLoader;

namespace PLang.Building.Parsers
{
	public class PrParser
	{
		private List<Goal> goals = null!;
		private List<Goal> publicGoals = null!;
		private List<Goal> systemGoals = null!;
		private List<Goal> runtimeEventsGoals = null!;
		private List<Goal> builderEventsGoals = null!;

		private readonly Dictionary<string, Instruction> instructions = new Dictionary<string, Instruction>();
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private bool disposed;

		public PrParser(IPLangFileSystem fileSystem, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.logger = logger;

			goals = GetGoals();
			systemGoals = GetSystemGoals();

			builderEventsGoals = GetEventsFiles(true);
			runtimeEventsGoals = GetEventsFiles(false);
		}


		public virtual Goal? ParsePrFile(string absolutePrFilePath)
		{
			if (!absolutePrFilePath.Contains(".pr"))
			{
				throw new ArgumentException($"path ({absolutePrFilePath} does not contain .pr file");
			}

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
			if (appsPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + "apps" + fileSystem.Path.DirectorySeparatorChar))
			{
				var paths = appsPath.Split(fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
				appsPath = fileSystem.Path.DirectorySeparatorChar + paths[0] + fileSystem.Path.DirectorySeparatorChar + paths[1];
				goal.AppName = paths[1];

				goal.RelativeAppStartupFolderPath = appsPath;
				goal.RelativeGoalFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativeGoalFolderPath));
				goal.RelativeGoalPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativeGoalPath));
				goal.RelativePrPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrPath));
				goal.RelativePrFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrFolderPath));
				goal.AbsoluteAppStartupFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appAbsoluteStartupPath, appsPath));

			}
			else if (appsPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + ".services" + fileSystem.Path.DirectorySeparatorChar))
			{
				int i = 0;
				var paths = appsPath.Split(fileSystem.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
				appsPath = fileSystem.Path.DirectorySeparatorChar + paths[0] + fileSystem.Path.DirectorySeparatorChar + paths[1];
				goal.AppName = paths[1];

				goal.RelativeAppStartupFolderPath = appsPath;
				goal.RelativeGoalFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativeGoalFolderPath));
				goal.RelativeGoalPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativeGoalPath));
				goal.RelativePrPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrPath));
				goal.RelativePrFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appsPath, goal.RelativePrFolderPath));
				goal.AbsoluteAppStartupFolderPath = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.Join(appAbsoluteStartupPath, appsPath));
			}
			else
			{
				goal.AppName = fileSystem.Path.DirectorySeparatorChar.ToString();

				goal.AbsoluteAppStartupFolderPath = appAbsoluteStartupPath;
				goal.RelativeAppStartupFolderPath = fileSystem.Path.DirectorySeparatorChar.ToString();
			}


			goal.AbsoluteGoalPath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativeGoalPath);
			goal.AbsoluteGoalFolderPath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativeGoalFolderPath);

			goal.AbsolutePrFilePath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativePrPath);
			goal.AbsolutePrFolderPath = fileSystem.Path.Join(appAbsoluteStartupPath, goal.RelativePrFolderPath);

			AdjustPathsToOS(goal);
			goal.IsSystem = absolutePrFilePath.Contains(fileSystem.SystemDirectory);

			
			for (int i = 0; i < goal.GoalSteps.Count; i++)
			{
				goal.GoalSteps[i].AbsolutePrFilePath = fileSystem.Path.Join(goal.AbsolutePrFolderPath, goal.GoalSteps[i].PrFileName).AdjustPathToOs();
				goal.GoalSteps[i].RelativePrPath = fileSystem.Path.Join(goal.RelativePrFolderPath, goal.GoalSteps[i].PrFileName).AdjustPathToOs();
				goal.GoalSteps[i].AppStartupPath = appAbsoluteStartupPath.AdjustPathToOs();
				goal.GoalSteps[i].Number = i;
				goal.GoalSteps[i].Index = i;

				//remove from memory uneeded data for runtime
				goal.GoalSteps[i].Goal = goal;
				goal.GoalSteps[i].LlmRequest = null;
				goal.GoalSteps[i].PrFile = null;
			}

			return goal;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
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
			if (instructions.TryGetValue(step.AbsolutePrFilePath, out var instruction))
			{
				return instruction;
			}

			instruction = JsonHelper.ParseFilePath<Instruction>(fileSystem, step.AbsolutePrFilePath);
			if (instruction != null)
			{
				instructions.TryAdd(step.AbsolutePrFilePath, instruction);
			}
			return instruction;
			
		}
		public List<Goal> ForceLoadAllGoals()
		{
			var goals = LoadAllGoals(true);
			return goals;
		}	

		public async Task<List<Goal>> GoalFromGoalsFolder(string appName, IFileAccessHandler fileAccessHandler)
		{
			var path = AppContext.BaseDirectory;
			await fileAccessHandler.ValidatePathResponse(appName, path, "y");
			// not using IPlangFileSystem here, we need to get the goal in the runtime folder
			var files = fileSystem.Directory.GetFiles(fileSystem.Path.Join(path, "Goals", ".build"), ISettings.GoalFileName, SearchOption.AllDirectories).ToList();			
			
			var goals = new List<Goal>();
			foreach (var file in files)
			{
				var goal = ParsePrFile(file);
				if (goal != null)
				{
					if (this.goals.FirstOrDefault(p => p.RelativePrPath == goal.RelativePrPath) == null)
					{
						this.goals.Add(goal);
					}
					if (goal.Visibility == Visibility.Public && publicGoals.FirstOrDefault(p => p.RelativePrPath == goal.RelativePrPath) == null)
					{
						publicGoals.Add(goal);
					}
				}
			}		

			return this.goals;
		}

		public List<Goal> LoadAllGoals(bool force = false)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"   -- Loading all goals(force:{force})");
			
			GetGoals(force);
			GetSystemGoals(force);

			if (force)
			{
				instructions.Clear();
			}

			logger.LogDebug($"   -- Done loading all goals - {stopwatch.ElapsedMilliseconds}"); 
			return goals;
		}

		public List<Goal> LoadAllGoalsByPath(string dir) { 

			string buildDir = fileSystem.Path.Join(dir, ".build");
			if (!fileSystem.Directory.Exists(buildDir))
			{
				return new List<Goal>();
			}
		
			var files = fileSystem.Directory.GetFiles(buildDir, ISettings.GoalFileName, SearchOption.AllDirectories).ToList();

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


			var goals = new List<Goal>();
			foreach (var file in files)
			{
				var goal = ParsePrFile(file);
				if (goal != null)
				{
					goals.Add(goal);
				}
			}
			
			return goals;
		}

		public List<Goal> LoadAppsByPath(string dir)
		{

			string appsDir = fileSystem.Path.Join(dir, "apps");
			List<string> files = new();
			if (fileSystem.Directory.Exists(appsDir))
			{
				var unsortedFiles = fileSystem.Directory.GetFiles(appsDir, ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
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
			return goals;
		}
		public List<Goal> GetAllGoals()
		{
			if (goals.Count > 0) return goals;

			LoadAllGoals();
			return goals;
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
		}

		public Goal? GetGoalByAppAndGoalName(string appStartupPath, string goalNameOrPath, Goal? callingGoal = null)
		{
			if (string.IsNullOrEmpty(appStartupPath))
			{
				throw new ArgumentNullException(nameof(appStartupPath));
			}
			if (string.IsNullOrEmpty(goalNameOrPath))
			{
				throw new ArgumentNullException(nameof(goalNameOrPath));
			}
			/*
			 * ProcessVideo - goal belonging to same appStartupPath, located in any folder, root first, then by alphabetical order of folders
			 * ui/List - in ui folder, 
			 * apps/HelloWorld - finds a goal located in apps named HelloWorld
			 * apps/Ffmpeg/Convert - maps to apps/Ffmpeg/Convert.goal
			 * if you want to use app, path must start with apps/
			 */
			appStartupPath = appStartupPath.AdjustPathToOs();
			if (appStartupPath == fileSystem.Path.DirectorySeparatorChar.ToString())
			{
				appStartupPath = fileSystem.RootDirectory;
			}
			goalNameOrPath = goalNameOrPath.AdjustPathToOs().Replace(".goal", "").Replace("!", "");

			if (appStartupPath != fileSystem.RootDirectory && !fileSystem.IsPathRooted(appStartupPath))
			{
				appStartupPath = appStartupPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar);
				if (!appStartupPath.StartsWith(fileSystem.Path.DirectorySeparatorChar.ToString()))
				{
					appStartupPath = fileSystem.Path.DirectorySeparatorChar.ToString() + appStartupPath;
				}
			}

			Goal? goal = null;

			// first check for goal inside same goal file as the calling goal
			if (callingGoal != null && !goalNameOrPath.Contains(fileSystem.Path.DirectorySeparatorChar))
			{
				goal = goals.FirstOrDefault(p => p.RelativeGoalFolderPath == callingGoal.RelativeGoalFolderPath && p.GoalName.Equals(goalNameOrPath, StringComparison.OrdinalIgnoreCase));
				if (goal != null) return goal;
			}

			// match goal from root, e.g. /Start
			if (goalNameOrPath.StartsWith(fileSystem.Path.DirectorySeparatorChar))
			{
				goal = goals.FirstOrDefault(p=> p.RelativePrFolderPath.Equals(fileSystem.Path.Join(".build", goalNameOrPath), StringComparison.OrdinalIgnoreCase));
				if (goal != null) return goal;
			}

			// match goal from calling goal, e.g. calling goal is in /ui/ folder, when goalNameOrPath is user/edit, it matches /ui/user/edit.goal
			if (callingGoal != null && !goalNameOrPath.StartsWith(fileSystem.Path.DirectorySeparatorChar))
			{
				var newGoalPath = fileSystem.Path.Join(".build", callingGoal.RelativeGoalFolderPath, goalNameOrPath);
				goal = goals.FirstOrDefault(p => p.RelativePrFolderPath.Equals(newGoalPath, StringComparison.OrdinalIgnoreCase));
				if (goal != null) return goal;
			}
			
			goal = goals.FirstOrDefault(p => p.RelativePrFolderPath.Equals(fileSystem.Path.Join(".build", goalNameOrPath), StringComparison.OrdinalIgnoreCase));
			if (goal != null) return goal;

			goal = goals.FirstOrDefault(p => goalNameOrPath.TrimStart(fileSystem.Path.DirectorySeparatorChar).Equals(fileSystem.Path.Join(p.RelativeGoalFolderPath, p.GoalName).TrimStart(fileSystem.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
			if (goal != null) return goal;

		
			var possibleGoals = goals.Where(p => p.RelativePrFolderPath.EndsWith(goalNameOrPath, StringComparison.OrdinalIgnoreCase)).ToList();
			if (possibleGoals.Count == 1) return possibleGoals[0];
			if (possibleGoals.Count > 1)
			{
				var goalNames = possibleGoals.Select(p => {
						return p.RelativeGoalPath;
					});
				throw new GoalNotFoundException($"{goalNameOrPath} Could not be found. There are {possibleGoals.Count} to choose from. {string.Join(",", goalNames)}", appStartupPath, goalNameOrPath);
			}

			return goal;
		}

		public List<Goal> GetGoals(bool force = false)
		{
			if (!force && goals != null) return goals;

			goals = LoadAllGoalsByPath(fileSystem.RootDirectory);
			publicGoals = goals.Where(p => p.Visibility == Visibility.Public).ToList();
			

			return goals;
		}

		public List<Goal> GetSystemGoals(bool force = false)
		{
			if (!force && systemGoals != null) return systemGoals;

			systemGoals = LoadAllGoalsByPath(fileSystem.SystemDirectory);
			
			return systemGoals;
		}
		public List<Goal> GetApps()
		{
			var groupedGoals = GetAllGoals().GroupBy(p => p.AppName);
			var goals = new List<Goal>();
			foreach (var groupedGoal in groupedGoals)
			{
				var goal = groupedGoal.FirstOrDefault();
				if (goal != null && goal.RelativeAppStartupFolderPath.StartsWith(fileSystem.Path.DirectorySeparatorChar + "apps"))
				{
					goals.Add(goal);
				}
			}
			return goals;
		}

		public async Task<(List<Goal>? Goals, IError? Error)> LoadAppPath(string appName, IFileAccessHandler fileAccessHandler)
		{
			var path = fileSystem.GoalsPath;
			var appPath = fileSystem.Path.Join(path, appName, ".build");

			List<string>? files = new();

			if (fileSystem.Directory.Exists(appPath)) {
				files = fileSystem.Directory.GetFiles(fileSystem.Path.Join(appPath, ".build"), ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
			}

			if (files.Count == 0)
			{
				appPath = fileSystem.Path.Join(fileSystem.SystemDirectory, appName, ".build");
				if (fileSystem.Directory.Exists(appPath))
				{
					files = fileSystem.Directory.GetFiles(appPath, ISettings.GoalFileName, SearchOption.AllDirectories).ToList();
				}
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
			if (goals.Count == 0) return (null, new Error($"App '{appName}' could not be found", "AppNotFound"));
			return (goals, null);
		}


		public (List<Instruction>? Instructions, IError? Error) GetInstructions(List<GoalStep> steps, string? functionName = null)
		{
			var instructions = new List<Instruction>();
			foreach (var step in steps)
			{
				var instruction = ParseInstructionFile(step);
				if (instruction == null) continue;

				if (instruction.Function.Name != functionName) continue;
				
				instructions.Add(instruction);
			}

			return (instructions, null);

		}

		public List<Goal> GetEventsFiles(bool builder = false)
		{
			if (builderEventsGoals != null && builder) return builderEventsGoals;
			if (runtimeEventsGoals != null && !builder) return runtimeEventsGoals;

			if (!fileSystem.Directory.Exists(fileSystem.BuildPath))
			{
				return [];
			}

			List<Goal> eventFiles = new();
			string eventsFolderName = (!builder) ? "Events" : "BuilderEvents";

			var eventsFolderPath = fileSystem.Path.Join(fileSystem.BuildPath, "events", eventsFolderName);
			var rootEventFilePath = fileSystem.Path.Join(eventsFolderPath, "00. Goal.pr");
			if (fileSystem.File.Exists(rootEventFilePath))
			{
				var goal = this.ParsePrFile(rootEventFilePath);
				if (goal != null)
				{
					eventFiles.Add(goal);
				}
			}
			//todo: hack, otherwise it will load events twice when building /system/
			if (fileSystem.BuildPath.EndsWith("/plang/system/.build".AdjustPathToOs())) return eventFiles;

			var osEventsPath = fileSystem.Path.Join(fileSystem.SystemDirectory, ".build", "events", eventsFolderName);
			var osEventFilePath = fileSystem.Path.Join(osEventsPath, "00. Goal.pr");
			if (fileSystem.File.Exists(osEventFilePath))
			{
				var goal = this.ParsePrFile(osEventFilePath);
				if (goal != null)
				{
					eventFiles.Add(goal);
				}
			}

			return eventFiles;

		}
	}
}
