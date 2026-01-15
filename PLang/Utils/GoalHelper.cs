using Markdig.Helpers;
using MiniExcelLibs.Utils;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;

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

			if (fileName.ToLower() == System.IO.Path.Join(rootDirectory, "setup.goal").ToLower()) return true;
			if (fileName.ToLower().StartsWith(System.IO.Path.Join(rootDirectory, "setup"))) return true;
			if (fileName.ToLower().StartsWith(System.IO.Path.Join(".build", "setup"))) return true;

			return false;
		}

		public static bool IsSetup(Goal goal)
		{
			var result = (goal.GoalFileName.Equals("Setup.goal", StringComparison.OrdinalIgnoreCase)) ||
				(goal.RelativeGoalFolderPath.Equals(System.IO.Path.Join(System.IO.Path.DirectorySeparatorChar.ToString(), "setup"), StringComparison.OrdinalIgnoreCase));
			return result;
		}

		internal static bool IsEvent(Goal goal)
		{
			return (goal.RelativeGoalPath.Equals("/events/events.goal".AdjustPathToOs(), StringComparison.OrdinalIgnoreCase) ||
				goal.RelativeGoalPath.Equals("/events/builderevents.goal".AdjustPathToOs(), StringComparison.OrdinalIgnoreCase));
		}

		public static string GetAppName(string goalToRun)
		{
			// apps/MyApp/Start.goal => MyApp
			goalToRun = goalToRun.AdjustPathToOs().TrimStart(System.IO.Path.DirectorySeparatorChar);

			string appName = goalToRun.Substring(goalToRun.IndexOf(System.IO.	Path.DirectorySeparatorChar) + 1);
			if (appName.Contains(System.IO.Path.DirectorySeparatorChar))
			{
				appName = appName.Substring(0, appName.IndexOf(System.IO.Path.DirectorySeparatorChar));
			}
			return appName;
		}



		public static string GetSpaceByParent(Goal goal)
		{
			int i = 0;
			var parent = goal.ParentGoal;
			
			while (parent != null)
			{
				parent = parent.ParentGoal;
				i++;

				if (i > 100)
				{
					Console.WriteLine($"To deep: ErrorHelper - goalName: {goal?.GoalName}");
					break;
				}
			}
			return new string(' ', i);
		}


		

		public static bool RunOnce(Goal goal)
		{
			if (goal.DataSourceName != null && goal.DataSourceName.Contains("%")) return false;
			return IsSetup(goal);			
		}


		public static bool IsPartOfCallStack(Goal goal, EndGoal endGoal)
		{
			if (endGoal.Step == null) return false;

			if (goal.RelativePrPath.Equals(endGoal.Step.Goal.RelativePrPath)) return true;
			int counter = 0;
			var parentGoal = endGoal.Step.Goal.ParentGoal;
			while (parentGoal != null)
			{
				if (goal.RelativePrPath.Equals(parentGoal.RelativePrPath)) return true;
				parentGoal = parentGoal.ParentGoal;

				if (counter++ > 100)
				{
					Console.WriteLine($"To deep: GoalHelper.IsPartOfCallStack - goalName: {goal?.GoalName}");
					break;
				}
			}
			return false;

		}

		public static (Goal?, IError?) GetGoalPath(GoalStep step, GoalToCallInfo goalToCall, List<Goal> appGoals, IReadOnlyList<Goal> systemGoals)
		{
			Goal? goal;
			string callingGoalRelativeFolderPath = step.Goal.RelativeGoalFolderPath;
			if (!string.IsNullOrEmpty(goalToCall.Path) && !goalToCall.Path.Contains(".goal"))
			{				
				goal = appGoals.FirstOrDefault(p => p.RelativePrPath.Equals(goalToCall.Path.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase));
				if (goal != null) return (goal, null);

				goal = systemGoals.FirstOrDefault(p => p.RelativePrPath.Equals(goalToCall.Path.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase));
				if (goal != null) return (goal, null);

				return (null, new Error($"{goalToCall.Name} could not be found. Searched for {goalToCall.Path}", Key: "GoalNotFound", StatusCode: 404) {  Step = step});
			}

			string goalToCallName = goalToCall.Name;
			string goalToCallPath = "";

			if (goalToCallName.Contains(".."))
			{
				//adjust for ../
				goalToCallName = goalToCallName.Substring(goalToCallName.LastIndexOf("/") + 1);
				goalToCallPath = goalToCall.Name.Substring(0, goalToCall.Name.LastIndexOf("/") + 1).AdjustPathToOs();

				goalToCallPath = MergePath(callingGoalRelativeFolderPath, goalToCallPath).AdjustPathToOs();

			}
			 else if (goalToCallName.Contains("/"))
			{
				goalToCallName = goalToCallName.Substring(goalToCallName.LastIndexOf("/") + 1);
				goalToCallPath = goalToCall.Name.Substring(0, goalToCall.Name.LastIndexOf("/") + 1).AdjustPathToOs();

				if (!goalToCall.Name.StartsWith("/"))
				{
					goalToCallPath = Path.Join(callingGoalRelativeFolderPath, goalToCallPath.TrimEnd(Path.DirectorySeparatorChar));
				}

			} else 
			{
				goalToCallPath = callingGoalRelativeFolderPath.AdjustPathToOs();
			}
			
			goalToCallName = goalToCallName.Replace(".goal", "");
			if (goalToCallPath != Path.DirectorySeparatorChar.ToString())
			{
				goalToCallPath = goalToCallPath.TrimEnd(Path.DirectorySeparatorChar);
			}

			(goal, var error) = GetMatchingGoal(appGoals, step, goalToCallPath, goalToCallName);
			if (goal != null) return (goal, null);
			if (error != null && error.StatusCode != 404) return (null, error);

			return GetMatchingGoal(systemGoals, step, goalToCallPath, goalToCallName);
		}


		private static (Goal?, IError?) GetMatchingGoal(IReadOnlyList<Goal> goals, GoalStep step, string goalToCallPath, string goalToCallName)
		{
			var foundGoals = goals.Where(p => p.RelativeGoalFolderPath.Equals(goalToCallPath, StringComparison.OrdinalIgnoreCase)
					&& p.GoalName.Equals(goalToCallName, StringComparison.OrdinalIgnoreCase));
			if (foundGoals.Count() == 1) return (foundGoals.First(), null);
			if (foundGoals.Count() == 0) return (null, new NotFoundError($"Could not find goal matching {goalToCallName}. Searched for it at {goalToCallPath}") {  Step = step });

			var goal = foundGoals.FirstOrDefault(p => p.RelativeGoalPath == step.Goal.RelativeGoalPath);
			if (goal != null) return (goal, null);

			return (null, new Error($"Found {foundGoals.Count()} goals. I dont know which to choose") {  Step = step});
		}

		static string MergePath(string currentRelativePath, string newPath) =>
					new Uri(new Uri($"file://{currentRelativePath}/"), newPath).AbsolutePath;
		 
		internal static (Goal?, IError?) GetGoal(string relativeGoalPath, string absoluteAppPath, GoalToCallInfo goalToCall, IReadOnlyList<Goal> appGoals, IReadOnlyList<Goal> systemGoals)
		{
			Goal? goal;
			if (!string.IsNullOrEmpty(goalToCall.Path))
			{
				goal = appGoals.FirstOrDefault(p => p.RelativePrPath.Equals(goalToCall.Path.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase));
				if (goal != null) return (goal, null);

				goal = systemGoals.FirstOrDefault(p => p.RelativePrPath.Equals(goalToCall.Path.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase));
				if (goal != null) return (goal, null);
			}

			var dirName = Path.GetDirectoryName(relativeGoalPath);
			var goalName = goalToCall.Name.AdjustPathToOs().Replace(".goal", "");

			var goalPath = (goalName.StartsWith(Path.DirectorySeparatorChar)) ? Path.DirectorySeparatorChar.ToString() : "";
			if (goalName.Contains(Path.DirectorySeparatorChar))
			{
				goalName = goalName.Substring(goalName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
				goalPath = goalToCall.Name.Substring(0, goalToCall.Name.LastIndexOf(goalName)).AdjustPathToOs();
			}

			string relativePath;
			// check if path starts with /, e.g. call goal /Start
			if (goalPath.StartsWith(Path.DirectorySeparatorChar))
			{
				relativePath = goalPath.TrimEnd(Path.DirectorySeparatorChar);
				if (string.IsNullOrEmpty(relativePath)) relativePath = Path.DirectorySeparatorChar.ToString();
			}
			else
			{
				var pathToGoal = Path.Join(dirName, goalPath);
				var absolutePath = Path.GetFullPath(Path.Join(absoluteAppPath, pathToGoal));
				relativePath = absolutePath.Replace(absoluteAppPath, "").TrimEnd(Path.DirectorySeparatorChar);
				if (string.IsNullOrEmpty(relativePath)) relativePath = Path.DirectorySeparatorChar.ToString();

				var extension = Path.GetExtension(absolutePath);
				if (!string.IsNullOrEmpty(extension))
				{
					relativePath = relativePath.Replace(extension, "");
				}
			} 

			goal = appGoals.FirstOrDefault(p => p.RelativeGoalFolderPath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
										&& p.GoalName.Equals(goalName, StringComparison.OrdinalIgnoreCase));
			if (goal != null) return (goal, null);

			goal = systemGoals.FirstOrDefault(p => p.RelativeGoalFolderPath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
										&& p.GoalName.Equals(goalName, StringComparison.OrdinalIgnoreCase));
			if (goal != null) return (goal, null);

			return (null, new BuilderError($"Could not find {goalToCall.Name}", Retry: false, Key: "GoalNotFound", StatusCode: 404));

		}

	}
}
