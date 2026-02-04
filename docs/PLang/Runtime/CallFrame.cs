namespace PLang.Runtime;

public readonly partial struct CallFrame
{
    public Goal? Goal { get; }
    public Step? Step { get; }
    public DateTime Timestamp { get; }
    
    // Derived properties
    public string? GoalPath => Goal?.Path;
    public int? LineNumber => Step?.LineNumber;
    public string? StepText => Step?.Text;
    
    public CallFrame(Goal? goal, Step? step)
    {
        Goal = goal;
        Step = step;
        Timestamp = DateTime.UtcNow;
    }
    
    public override string ToString()
    {
        if (Step != null)
            return $"  at {GoalPath} line {LineNumber}: {StepText}";
        if (Goal != null)
            return $"  at {GoalPath}";
        return "  at <unknown>";
    }
}
