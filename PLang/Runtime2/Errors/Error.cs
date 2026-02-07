using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Generic base error class implementing IError.
/// Captures execution context (goal name, step) when provided.
/// </summary>
public class Error : IError
{
    public string Id { get; }
    public string Message { get; }
    public string Key { get; }
    public int StatusCode { get; }
    public string? FixSuggestion { get; init; }
    public string? HelpfulLinks { get; init; }
    public DateTime CreatedUtc { get; }
    public Exception? Exception { get; init; }
    public IError? InnerError { get; init; }

    public string? GoalName { get; init; }
    public int? StepIndex { get; init; }

    public Error(string message, string key = "Error", int statusCode = 400)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = DateTime.UtcNow;
    }

    public Error(string message, PLangContext context, string key = "Error", int statusCode = 400)
        : this(message, key, statusCode)
    {
        GoalName = context.CurrentGoalName;
        StepIndex = context.CurrentStepIndex;
    }

    public static Error FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? FromException(ex.InnerException) : null
        };
    }

    public static Error FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, context, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? FromException(ex.InnerException) : null
        };
    }

    public override string ToString() => $"[{Key}] {Message}";
}
