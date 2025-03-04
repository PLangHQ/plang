using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;

namespace PLang.Errors.Methods
{
	public record InvalidParameterError(string FunctionName, string Message, GoalStep Step, int StatusCode = 500, string? FixSuggestion = null, string? HelpfulLinks = null) : StepError(Message, Step, "InvalidParameter", StatusCode, FixSuggestion: FixSuggestion, HelpfulLinks: HelpfulLinks)
	{
		public override string ToString()
		{
			return base.ToString();
		}
	}
}
