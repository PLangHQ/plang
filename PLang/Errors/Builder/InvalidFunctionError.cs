namespace PLang.Errors.Builder;

public record InvalidFunctionsError(
    string FunctionName,
    string Message,
    bool ExcludeModule,
    string? FixSuggestion = null,
    string? HelpfulLinks = null) : BuilderError(Message, "InvalidFunction", FixSuggestion: FixSuggestion,
    HelpfulLinks: HelpfulLinks)
{
    public override string ToString()
    {
        return base.ToString();
    }
}