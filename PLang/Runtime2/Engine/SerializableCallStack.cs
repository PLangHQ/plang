namespace PLang.Runtime2.Engine;

/// <summary>
/// Serializable representation of the call stack.
/// </summary>
public sealed class SerializableCallStack
{
    public List<SerializableCallFrame> Frames { get; set; } = new();
    public int Depth { get; set; }
    public string StackTrace { get; set; } = "";
}

/// <summary>
/// Serializable representation of a call frame.
/// </summary>
public sealed class SerializableCallFrame
{
    public string Id { get; set; } = "";
    public string GoalName { get; set; } = "";
    public string? GoalPath { get; set; }
    public string Phase { get; set; } = "";
    public int CurrentStepIndex { get; set; }
    public string? CurrentStepText { get; set; }
    public DateTime StartedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public int Depth { get; set; }
    public bool HasErrors { get; set; }
}
