using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Builder
{
	public record StepBuilderError(string Message, GoalStep Step, string Key = "StepBuilder", int StatusCode = 400, bool ContinueBuild = true, Exception? ex = null, string? FixSuggestion = null, string? HelpfulLinks = null) : GoalBuilderError(Message, Step.Goal, Key, StatusCode, ContinueBuild, ex, FixSuggestion, HelpfulLinks)
	{
		public override string ToString()
		{
			return base.ToString();
		}
	}

}
