using PLang.Building.Model;

namespace PLang.Errors.Builder;

public record GoalBuilderError(
    string Message,
    Goal Goal,
    string Key = "GoalBuilder",
    int StatusCode = 400,
    bool ContinueBuild = true,
    Exception? Exception = null,
    string? FixSuggestion = null,
    string? HelpfulLinks = null)
    : BuilderError(Message, Key, StatusCode, ContinueBuild, Exception, FixSuggestion, HelpfulLinks)
{
    public override Goal Goal { get; set; } = Goal;

    public override string ToString()
    {
        return base.ToString();
    }
}