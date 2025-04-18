﻿using MiniExcelLibs.Utils;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;

namespace PLang.Utils
{
	public class GoalHelper
	{
		public static bool IsSetup(GoalStep? step)
		{
			if (step == null) return false;

			return IsSetup(step.Goal.AbsoluteAppStartupFolderPath, step.RelativePrPath);
		}
		public static bool IsSetup(string rootDirectory, string fileName)
		{

			if (fileName.ToLower() == Path.Join(rootDirectory, "setup.goal").ToLower()) return true;
			if (fileName.ToLower().StartsWith(Path.Join(rootDirectory, "setup"))) return true;
			if (fileName.ToLower().StartsWith(Path.Join(".build", "setup"))) return true;

			return false;
		}

		public static List<string> GetGoalFilesToBuild(IPLangFileSystem fileSystem, string goalPath)
		{
			string[] anyFile = fileSystem.Directory.GetFiles(goalPath, "*.goal", SearchOption.AllDirectories);
			if (anyFile.Length == 0)
			{
				throw new BuilderException($"No goal files found in directory. Are you in the correct directory? I am running from {goalPath}");
			}
			var goalFiles = fileSystem.Directory.GetFiles(goalPath, "*.goal", SearchOption.AllDirectories).ToList();
			return Remove_SystemFolder(goalPath, goalFiles);
		}


		private static List<string> Remove_SystemFolder(string goalPath, List<string> goalFiles)
		{


			string[] dirsToExclude = new string[] { "apps", ".modules", ".services", ".build", ".deploy", ".db" };
			string[] filesToExclude = new string[] { "Events.goal", "BuilderEvents.goal" };


			// Filter out excluded directories and files first to simplify subsequent operations
			var filteredGoalFiles = goalFiles.Where(goalFile =>
			{
				var relativePath = goalFile.Replace(goalPath, "").TrimStart(Path.DirectorySeparatorChar);
				var baseFolderName = Path.GetDirectoryName(relativePath).Split(Path.DirectorySeparatorChar).FirstOrDefault();
				var fileName = Path.GetFileName(goalFile).ToLower();

				return !baseFolderName.StartsWith(".") && !dirsToExclude.Contains(baseFolderName, StringComparer.OrdinalIgnoreCase) && !filesToExclude.Contains(fileName, StringComparer.OrdinalIgnoreCase);
			}).ToList();

			// Order the files
			var orderedFiles = filteredGoalFiles
				.OrderBy(file => !file.Contains(Path.Join(goalPath, "events"), StringComparison.OrdinalIgnoreCase))  // "events" folder first
				.ThenBy(file => Path.GetFileName(file).ToLower() != "setup.goal") // "setup.goal" second
				.ThenBy(file => !file.Contains(Path.Join(goalPath, "setup", Path.DirectorySeparatorChar.ToString()), StringComparison.OrdinalIgnoreCase))   
				.ThenBy(file => Path.GetFileName(file).ToLower() != "start.goal")
				.ToList();


			return orderedFiles;
		}


		public static string GetAppName(string goalToRun)
		{
			// apps/MyApp/Start.goal => MyApp
			goalToRun = goalToRun.AdjustPathToOs().TrimStart(Path.DirectorySeparatorChar);

			string appName = goalToRun.Substring(goalToRun.IndexOf(Path.DirectorySeparatorChar) + 1);
			if (appName.Contains(Path.DirectorySeparatorChar))
			{
				appName = appName.Substring(0, appName.IndexOf(Path.DirectorySeparatorChar));
			}
			return appName;
		}

		public static string GetGoalPath(string goalToRun)
		{
			// apps/MyApp/ => Start
			// apps/MyApp/Start => Start
			// apps/MyApp/Process => Process
			// apps/MyApp/Process/MoreStuff => Process/MoreStuff

			goalToRun = goalToRun.AdjustPathToOs().TrimStart(Path.DirectorySeparatorChar);
			var appName = GetAppName(goalToRun);

			string goalPath = goalToRun.Substring(goalToRun.IndexOf(appName) + appName.Length).TrimStart(Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
			if (string.IsNullOrEmpty(goalPath))
			{
				return "Start";
			}

			return goalPath;
		}


		public static string GetSpaceByParent(Goal goal)
		{
			int i = 0;
			var parent = goal.ParentGoal;
			while (parent != null)
			{
				parent = parent.ParentGoal;
				i++;
			}
			return new string(' ', i);
		}


		public static bool IsSetup(Goal goal)
		{
			return (goal.GoalFileName.Equals("Setup.goal", StringComparison.OrdinalIgnoreCase)) || (goal.RelativeGoalFolderPath.Equals(Path.Join(Path.DirectorySeparatorChar.ToString(), "setup"), StringComparison.OrdinalIgnoreCase));
		}
	}
}
