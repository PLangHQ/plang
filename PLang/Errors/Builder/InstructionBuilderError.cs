using Castle.Components.DictionaryAdapter.Xml;
using PLang.Building.Model;

namespace PLang.Errors.Builder
{
	public record InstructionBuilderError(string Message, GoalStep Step, Instruction Instruction, string Key = "InstructionBuilder", int StatusCode = 400,
		bool ContinueBuild = true, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null, bool Retry = true, string? LlmBuilderHelp = null) : 
		StepBuilderError(Message, Step, Key, StatusCode, ContinueBuild, Exception, FixSuggestion, HelpfulLinks, Retry, LlmBuilderHelp)
	{
		public override string ToString()
		{
			return base.ToString();
		}
	}

}
