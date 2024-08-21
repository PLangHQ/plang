using PLang.Building.Model;

namespace PLang.Errors.Runtime
{
	public record UserDefinedError(string Message, GoalStep Step, string Key = "StepError", int StatusCode = 400,
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null)
			: StepError(Message, Step, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
	{
		public override string ToString()
		{
			return Message; 
		}
	}
}
