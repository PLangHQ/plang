using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Runtime
{

	public record StepError(string Message, GoalStep Step, string Key = "StepError", int StatusCode = 400,
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) 
			: GoalError(Message, Step.Goal, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
	{
	}
}
