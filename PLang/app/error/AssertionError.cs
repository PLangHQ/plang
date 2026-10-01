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

    public AssertionError(string message, string key = "AssertionFailed", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    /// <summary>A failed assertion of <paramref name="actual"/> against <paramref name="expected"/>, its
    /// <paramref name="message"/> formed by the assertion that compared them.</summary>
    public AssertionError(string message, object? expected, object? actual, string? userMessage)
        : base(message, "AssertionFailed", 400)
    {
        Expected = expected;
        Actual = actual;
        UserMessage = userMessage;
    }

    /// <summary>An assertion always keeps the variables it failed among — for the failure's report.</summary>
    protected internal override bool Keeps(actor.context.@this context) => true;
}
