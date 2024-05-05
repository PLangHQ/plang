﻿using PLang.Building.Model;

namespace PLang.Errors.Builder
{
	public record CompilerError(string Message, string LlmInstruction, GoalStep Step, string Key = "CompilerError", int StatusCode = 500, bool ContinueBuild = true, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) : StepBuilderError(Message, Step, Key, StatusCode, ContinueBuild, Exception, FixSuggestion, HelpfulLinks)
	{
	}
}
