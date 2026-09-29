namespace app.error;

/// <summary>
/// Error for assertion failures in PLang tests.
/// Captures expected vs actual values for clear failure reporting.
/// </summary>
public class AssertionError : Error
{
    public object? Expected { get; init; }
    public object? Actual { get; init; }
    public string? UserMessage { get; init; }

    public AssertionError(string message, string key = "AssertionFailed", int statusCode = 400)
        : base(message, key, statusCode) { }

    private AssertionError(object? expected, object? actual, string? userMessage, string message)
        : base(message, "AssertionFailed", 400)
    {
        Expected = expected;
        Actual = actual;
        UserMessage = userMessage;
    }

    /// <summary>The failure of <paramref name="actual"/> against <paramref name="expected"/>: its message shows
    /// both as a diagnostic does (a secret masked), after <paramref name="userMessage"/> when there is one.</summary>
    public static async System.Threading.Tasks.Task<AssertionError> Of(object? expected, object? actual, string? userMessage,
        actor.context.@this context)
    {
        var shownExpected = await global::app.Diagnostics.Format.Value(expected, context);
        var shownActual = await global::app.Diagnostics.Format.Value(actual, context);
        var message = $"Expected: {shownExpected}, Actual: {shownActual}";
        if (!string.IsNullOrEmpty(userMessage))
            message = $"{userMessage} — {message}";
        return new AssertionError(expected, actual, userMessage, message);
    }

    /// <summary>An assertion always keeps the variables it failed among — for the failure's report.</summary>
    protected internal override bool Keeps(actor.context.@this context) => true;
}
