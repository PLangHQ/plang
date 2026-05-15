namespace app.Errors;

/// <summary>
/// Per-parameter snapshot captured at the point a handler returned an error.
/// Populated by the source-generated ExecuteAsync — same data the handler itself saw.
/// Lets the user (or LLM auto-fix) see "param X arrived as Y" without re-running.
/// </summary>
public sealed record ParamSnapshot
{
    /// <summary>Property name as declared on the handler (e.g. "Messages").</summary>
    public required string Name { get; init; }

    /// <summary>Declared CLR type (e.g. "Data&lt;List&lt;LlmMessage&gt;&gt;"). For display.</summary>
    public string? DeclaredType { get; init; }

    /// <summary>What the .pr provided as the raw value before any resolution. Often a "%var%" string.</summary>
    public object? PrValue { get; init; }

    /// <summary>What the .pr declared as the type for this parameter (e.g. "list&lt;llmmessage&gt;").</summary>
    public string? PrType { get; init; }

    /// <summary>The final value the property holds after lazy resolution. Null if the property was never accessed or resolved to null.</summary>
    public object? FinalValue { get; init; }

    /// <summary>True if the property has been accessed (lazy backing field is set). Distinguishes "never read" from "read and is null".</summary>
    public bool WasAccessed { get; init; }
}
