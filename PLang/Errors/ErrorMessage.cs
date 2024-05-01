using PLang.Building.Model;

namespace PLang.Errors
{
	public record ErrorMessage(string message, string type, int statusCode, GoalStep step) : StepError(message, step, "UserStepError")
	{
		public string Type { get; } = type;
		public int StatusCode { get; } = statusCode;
	}
}
