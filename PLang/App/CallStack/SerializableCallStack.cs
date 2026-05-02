namespace App.CallStack;

/// <summary>
/// Serializable representation of the call stack — a snapshot of the active Caller chain
/// from the current leaf to the root. Used by trace exporters and the JSON debug payload;
/// the live <see cref="Call.@this"/> tree stays in memory.
/// </summary>
public sealed class SerializableCallStack
{
    public List<SerializableCall> Frames { get; set; } = new();
    public int Depth { get; set; }
}

/// <summary>
/// Serializable representation of one Call frame. Snake-cased field names match the
/// JSON wire format used by the trace exporter.
/// </summary>
public sealed class SerializableCall
{
    public string Id { get; set; } = "";
    public string GoalName { get; set; } = "";
    public string? GoalPath { get; set; }
    public int CurrentStepIndex { get; set; }
    public string? CurrentStepText { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool HasErrors { get; set; }
    public bool Handled { get; set; }
}
