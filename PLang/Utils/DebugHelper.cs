using PLang.Interfaces;

namespace PLang.Utils
{
	public static class DebugHelper
	{
		public static object? GetDebugInfo(PLangContext context)
		{
			if (context.CallingStep == null) return null;

			var step = context.CallingStep;
			var goal = step.Goal;

			return new
			{
				goal = new { name = goal.GoalName, path = goal.RelativeGoalPath, absolutePath = goal.AbsoluteGoalPath },
				step = new { text = step.Text, step.Stopwatch, line = context.CallingStep.LineNumber }
			};
		}
	}
}
