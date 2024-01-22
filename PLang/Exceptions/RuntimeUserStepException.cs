using PLang.Building.Model;

namespace PLang.Exceptions
{
	public class RuntimeUserStepException : Exception
	{
		public GoalStep? Step { get; set; }
		public string Type { get; set; }
		public int StatusCode { get; set; }
		public RuntimeUserStepException(string message, string type, int statusCode, GoalStep? step) : base(message) {
			this.Step = step;
			this.Type = type;
			this.StatusCode = statusCode;
		}
		public RuntimeUserStepException(GoalStep step, Exception ex, string type = "error", int statusCode = 500) : base($"Step '{step.Text}' had exception", ex) {
			this.Step = step;
			this.Type = type;
			this.StatusCode = statusCode;
		}
	}
}
