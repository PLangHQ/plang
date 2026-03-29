using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Goals.Goal;

/// <summary>
/// Strongly-typed reference to a goal, carrying name, parameters, and optional pre-resolved PrPath.
/// PrPath is nullable because dynamic goal names (containing %variable%) can't resolve at build time.
/// </summary>
public sealed class GoalCall
{
    /// <summary>Goal name to call (e.g., "ProcessData", "/Setup/Init").</summary>
    [Store, LlmBuilder]
    public string Name { get; init; } = "";
    /// <summary>Description of what this goal does — used when GoalCall is a tool definition for an LLM.</summary>
    [Store, LlmBuilder]
    public string? Description { get; init; }

    /// <summary>Whether this tool is safe for concurrent execution. Default false.</summary>
    [Store, LlmBuilder]
    public bool Parallel { get; init; }

    /// <summary>Parameters to pass to the goal, each as a named Data value.</summary>
    [Store, LlmBuilder]
    public List<Data>? Parameters { get; init; }
    /// <summary>Pre-resolved .pr file path. Null when the goal name contains %variables%.</summary>
    [Store]
    public string? PrPath { get; set; }
}
