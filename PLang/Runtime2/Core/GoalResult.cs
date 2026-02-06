using PLang.Runtime2.Errors;

namespace PLang.Runtime2.Core;

/// <summary>
/// Central return type for all Runtime2 operations.
/// Replaces the pattern of returning Task&lt;object?&gt; or tuples.
/// </summary>
public sealed class GoalResult
{
    public bool Success { get; }
    public object? Value { get; }
    public ErrorInfo? Error { get; }

    private GoalResult(bool success, object? value, ErrorInfo? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result with no value.
    /// </summary>
    public static GoalResult Ok() => new(true, null, null);

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static GoalResult Ok(object? value) => new(true, value, null);

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static GoalResult Fail(ErrorInfo error) => new(false, null, error);

    /// <summary>
    /// Creates an error result from a message.
    /// </summary>
    public static GoalResult Fail(string message, string key = "Error", int statusCode = 400)
        => new(false, null, new ErrorInfo(message, key, statusCode));

    /// <summary>
    /// Returns a completed Task containing a successful result with no value.
    /// </summary>
    public static Task<GoalResult> SuccessTask() => Task.FromResult(Ok());

    /// <summary>
    /// Returns a completed Task containing a successful result with a value.
    /// </summary>
    public static Task<GoalResult> SuccessTask(object? value) => Task.FromResult(Ok(value));

    /// <summary>
    /// Returns a completed Task containing an error result.
    /// </summary>
    public static Task<GoalResult> ErrorTask(ErrorInfo error) => Task.FromResult(Fail(error));

    /// <summary>
    /// Returns a completed Task containing an error result from a message.
    /// </summary>
    public static Task<GoalResult> ErrorTask(string message, string key = "Error", int statusCode = 400)
        => Task.FromResult(Fail(message, key, statusCode));

    /// <summary>
    /// Gets the value cast to the specified type, or default if null or wrong type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (Value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Implicit conversion to bool for easy success checking.
    /// </summary>
    public static implicit operator bool(GoalResult result) => result.Success;

    public override string ToString()
    {
        if (Success)
            return Value?.ToString() ?? "Success (no value)";
        return $"Error: {Error?.Message ?? "Unknown error"}";
    }
}
