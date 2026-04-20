using System.Text;

namespace App.Errors;

/// <summary>
/// Error for assertion failures in PLang tests.
/// Captures expected vs actual values for clear failure reporting.
/// </summary>
public class AssertionError : Error
{
    public override ErrorCategory Category => ErrorCategory.Application;
    public object? Expected { get; init; }
    public object? Actual { get; init; }
    public string? UserMessage { get; init; }

    /// <summary>
    /// Snapshot of user-visible variables at the moment of failure, captured by assert
    /// handlers on failure only. Null on fresh construction and on passing assertions.
    /// Consumed by the runner's failure-diagnostic renderer (test.report).
    /// </summary>
    public Dictionary<string, object?>? Variables { get; set; }

    public AssertionError(string message, string key = "AssertionFailed", int statusCode = 400)
        : base(message, key, statusCode) { }

    public AssertionError(object? expected, object? actual, string? userMessage = null)
        : base(FormatMessage(expected, actual, userMessage), "AssertionFailed", 400)
    {
        Expected = expected;
        Actual = actual;
        UserMessage = userMessage;
    }

    private static string FormatMessage(object? expected, object? actual, string? userMessage)
    {
        var msg = $"Expected: {FormatValue(expected)}, Actual: {FormatValue(actual)}";
        if (!string.IsNullOrEmpty(userMessage))
            msg = $"{userMessage} — {msg}";
        return msg;
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "(null)";
    }

    protected override void FormatExtra(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}  Expected: {FormatValue(Expected)}");
        sb.AppendLine($"{indent}  Actual:   {FormatValue(Actual)}");
    }
}
