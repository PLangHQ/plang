namespace app.error;

/// <summary>
/// Error for assertion failures in PLang tests.
/// Captures expected vs actual values for clear failure reporting.
/// </summary>
public class AssertionError : Error
{
    /// <summary>What the assertion expected, as it read and showed it in the test's own context — a template's
    /// variables filled there, never again where the failure is reported.</summary>
    public string? Expected { get; init; }
    /// <summary>What the assertion got, as it read and showed it in the test's own context.</summary>
    public string? Actual { get; init; }
    public string? UserMessage { get; init; }

    public AssertionError(string message, string key = "AssertionFailed", global::app.type.item.status.@this? status = null)
        : base(message, key, status) { }

    /// <summary>A failed assertion of <paramref name="actual"/> against <paramref name="expected"/>, each as the
    /// assertion showed it, its <paramref name="message"/> formed by the assertion that compared them.</summary>
    public AssertionError(string message, string? expected, string? actual, string? userMessage)
        : base(message, "AssertionFailed", 400)
    {
        Expected = expected;
        Actual = actual;
        UserMessage = userMessage;
    }

    /// <summary>An assertion always keeps the variables it failed among — for the failure's report.</summary>
    protected internal override bool Keeps(actor.context.@this context) => true;
}
