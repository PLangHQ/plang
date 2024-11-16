using PLang.Building.Model;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors.Runtime;

public record ProgramError(
    string Message,
    GoalStep Step,
    GenericFunction GenericFunction,
    Dictionary<string, object?>? ParameterValues = null,
    string Key = "ProgramError",
    int StatusCode = 400,
    Exception? Exception = null,
    string? FixSuggestion = null,
    string? HelpfulLinks = null)
    : StepError(Message, Step, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
{
    public GenericFunction GenericFunction { get; set; } = GenericFunction;

    public override string ToString()
    {
        return base.ToString();
    }
}