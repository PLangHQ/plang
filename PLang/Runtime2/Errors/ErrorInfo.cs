namespace PLang.Runtime2.Errors;

/// <summary>
/// Lightweight error information structure for Runtime2.
/// Designed to be simple and serializable.
/// </summary>
public sealed class ErrorInfo
{
    public string Id { get; }
    public string Message { get; }
    public string Key { get; }
    public int StatusCode { get; }
    public string? FixSuggestion { get; init; }
    public string? HelpfulLinks { get; init; }
    public DateTime CreatedUtc { get; }
    public Exception? Exception { get; init; }
    public ErrorInfo? InnerError { get; init; }

    public ErrorInfo(string message, string key = "Error", int statusCode = 400)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates an ErrorInfo from an exception.
    /// </summary>
    public static ErrorInfo FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ErrorInfo(ex.Message, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? FromException(ex.InnerException) : null
        };
    }

    /// <summary>
    /// Creates an ErrorInfo for a not found scenario.
    /// </summary>
    public static ErrorInfo NotFound(string what) => new($"{what} not found", "NotFound", 404);

    /// <summary>
    /// Creates an ErrorInfo for invalid input.
    /// </summary>
    public static ErrorInfo InvalidInput(string message) => new(message, "InvalidInput", 400);

    /// <summary>
    /// Creates an ErrorInfo for unauthorized access.
    /// </summary>
    public static ErrorInfo Unauthorized(string message = "Unauthorized") => new(message, "Unauthorized", 401);

    public override string ToString() => $"[{Key}] {Message}";
}
