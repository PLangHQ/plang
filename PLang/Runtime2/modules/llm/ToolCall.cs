namespace PLang.Runtime2.modules.llm;

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
}
