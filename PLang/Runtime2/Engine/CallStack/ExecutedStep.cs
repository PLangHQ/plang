namespace PLang.Runtime2.Engine.CallStack;

/// <summary>
/// Record of an executed step. Stores the Step reference (OBP rule #3).
/// </summary>
public sealed class ExecutedStep
{
    public Step Step { get; }
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }

    public ExecutedStep(Step step)
    {
        Step = step;
        StartedAt = DateTime.UtcNow;
    }
}
