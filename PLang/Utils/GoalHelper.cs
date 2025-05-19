using MiniExcelLibs.Utils;
using PLang.Building.Model;
using PLang.Errors;
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
			var result = (goal.GoalFileName.Equals("Setup.goal", StringComparison.OrdinalIgnoreCase)) || (goal.RelativeGoalFolderPath.Equals(Path.Join(Path.DirectorySeparatorChar.ToString(), "setup"), StringComparison.OrdinalIgnoreCase));
			return result;
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

			var parentGoal = endGoal.Step.Goal.ParentGoal;
			while (parentGoal != null)
			{
				if (goal.RelativePrPath.Equals(parentGoal.RelativePrPath)) return true;
				parentGoal = parentGoal.ParentGoal;
			}
			return false;

		}
	}
}
