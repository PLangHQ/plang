namespace PLang.Errors.Builder
{
	public record InvalidFunctionsError(string FunctionName, string Explain, bool ExcludeModule, string? FixSuggestion = null, string? HelpfulLinks = null) : MultipleBuildError("InvalidFunction");
}
