namespace PLang.Runtime2.Errors;

/// <summary>
/// Interface for all Runtime2 error types.
/// </summary>
public interface IError
{
    string Id { get; }
    string Message { get; }
    string Key { get; }
    int StatusCode { get; }
    string? FixSuggestion { get; }
    string? HelpfulLinks { get; }
    DateTime CreatedUtc { get; }
    Exception? Exception { get; }
    IError? InnerError { get; }

    /// <summary>
    /// Goal name where the error occurred (if known).
    /// </summary>
    string? GoalName { get; }

    /// <summary>
    /// Step index where the error occurred (if known).
    /// </summary>
    int? StepIndex { get; }

}
