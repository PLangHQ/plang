namespace PLang.Runtime2.Engine;

/// <summary>
/// Strongly-typed reference to a goal, carrying name, parameters, and optional pre-resolved PrPath.
/// PrPath is nullable because dynamic goal names (containing %variable%) can't resolve at build time.
/// </summary>
public sealed class GoalCall
{
    [Store, LlmBuilder]
    public string Name { get; init; } = "";
    [Store, LlmBuilder]
    public Dictionary<string, object?>? Parameters { get; init; }
    [Store]
    public string? PrPath { get; set; }
}
