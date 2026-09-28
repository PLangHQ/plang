namespace app.module.llm;

/// <summary>
/// Minimal carrier for an LLM tool call response.
/// The provider parses its API response into these; the tool loop uses them
/// to find matching GoalCalls and execute.
/// </summary>
public class ToolCall
{
    /// <summary>Provider-assigned call ID.</summary>
    public string Id { get; set; } = "";

    /// <summary>Goal name to call.</summary>
    public string Name { get; set; } = "";

    /// <summary>JSON string of arguments from the LLM.</summary>
    public string Arguments { get; set; } = "";

    /// <summary>What an OnToolCall callback is handed about this call: its name and arguments, where it is
    /// (<paramref name="status"/>), and — once it ran — its <paramref name="result"/>.</summary>
    public IEnumerable<global::app.data.@this> State(string status, string? result, global::app.actor.context.@this context)
    {
        yield return new global::app.data.@this("name", Name, context: context);
        yield return new global::app.data.@this("arguments", Arguments, context: context);
        yield return new global::app.data.@this("status", status, context: context);
        if (result != null) yield return new global::app.data.@this("result", result, context: context);
    }
}
