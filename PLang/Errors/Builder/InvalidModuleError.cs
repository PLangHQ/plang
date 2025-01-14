namespace PLang.Errors.Builder
{
	public record InvalidModuleError(string ModuleType, string Message, string? FixSuggestion = null, string? HelpfulLinks = null) : BuilderError(Message, "InvalidModule", FixSuggestion: FixSuggestion, HelpfulLinks: HelpfulLinks)
	{
		public override string ToString()
		{
			return base.ToString();
		}
	}
}
