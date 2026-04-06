using App.Engine.Context;

namespace App.Engine.Errors;

/// <summary>
/// Error for input validation failures.
/// Example: invalid parameter value, type mismatch, out-of-range value.
/// </summary>
public class ValidationError : Error
{
    public override ErrorCategory Category => ErrorCategory.Application;
    public string? ParameterName { get; init; }

    public ValidationError(string message, string key = "ValidationError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ValidationError(string message, Step step, string key = "ValidationError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public ValidationError(string message, PLangContext context, string key = "ValidationError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public static ValidationError Required(string parameterName) =>
        new($"'{parameterName}' is required", "MissingParameter", 400) { ParameterName = parameterName };

    public static ValidationError InvalidType(string parameterName, string expectedType) =>
        new($"'{parameterName}' must be of type {expectedType}", "InvalidType", 400) { ParameterName = parameterName };

    public static ValidationError OutOfRange(string parameterName, string constraint) =>
        new($"'{parameterName}' is out of range: {constraint}", "OutOfRange", 400) { ParameterName = parameterName };
}
