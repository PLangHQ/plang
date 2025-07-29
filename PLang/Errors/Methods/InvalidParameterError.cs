using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;

namespace PLang.Errors.Methods
{
	public record InvalidParameterError(string FunctionName, string Message, GoalStep Step, string Key = "InvalidParameter",
		Exception? Exception = null, int StatusCode = 500, string? FixSuggestion = null, string? HelpfulLinks = null) : 
		StepError(Message, Step, Key, StatusCode, FixSuggestion: FixSuggestion, HelpfulLinks: HelpfulLinks, Exception: Exception)
	{
		public override string ToString()
		{
			return base.ToString();
		}
	}
}
