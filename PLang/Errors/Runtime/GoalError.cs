using PLang.Building.Model;

namespace PLang.Errors.Runtime;

public record GoalError(
    string Message,
    Goal Goal,
    string Key = "GoalError",
    int StatusCode = 400,
    Exception? Exception = null,
    string? FixSuggestion = null,
    string? HelpfulLinks = null)
    : Error(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
{
    public override Goal? Goal { get; set; } = Goal;

    public override string ToString()
    {
        return base.ToString();
    }
}