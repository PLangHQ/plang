namespace PLang.Runtime2.Engine.CallStack;

/// <summary>
/// Record of an executed step.
/// </summary>
public sealed class ExecutedStep
{
    public int Index { get; init; }
    public string Text { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
}
