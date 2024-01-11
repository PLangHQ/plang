using PLang.Building.Model;

namespace PLang.Exceptions
{
	public class RuntimeGoalEndException : Exception
	{
		public GoalStep? Step { get; set; }
		public RuntimeGoalEndException(string? message, GoalStep? step) : base(message)
		{
			this.Step = step;
		}
		public RuntimeGoalEndException(GoalStep? step, Exception? ex) : base($"Step '{step?.Text}' ended goal", ex)
		{
			this.Step = step;
		}
	}
}
