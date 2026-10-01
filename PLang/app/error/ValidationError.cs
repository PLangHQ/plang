using app.actor.context;

namespace app.error;

/// <summary>
/// Error for input validation failures.
/// Example: invalid parameter value, type mismatch, out-of-range value.
/// </summary>
public class ValidationError : Error
{
    public string? ParameterName { get; init; }

    public ValidationError(string message, string key = "ValidationError", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    public ValidationError(string message, Step step, string key = "ValidationError", global::app.type.item.status.@this? status = null)
        : base(message, step, key, status) { }

    public ValidationError(string message, actor.context.@this context, string key = "ValidationError", global::app.type.item.status.@this? status = null)
        : base(message, context, key, status) { }

    public static ValidationError Required(string parameterName) =>
        new($"'{parameterName}' is required", "MissingParameter", 400) { ParameterName = parameterName };

    public static ValidationError InvalidType(string parameterName, string expectedType) =>
        new($"'{parameterName}' must be of type {expectedType}", "InvalidType", 400) { ParameterName = parameterName };

    public static ValidationError OutOfRange(string parameterName, string constraint) =>
        new($"'{parameterName}' is out of range: {constraint}", "OutOfRange", 400) { ParameterName = parameterName };
}
