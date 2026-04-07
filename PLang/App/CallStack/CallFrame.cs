using System.Diagnostics;
using App.Errors;
using Goal = App.Goals.Goal.@this;

namespace App.CallStack;

/// <summary>
/// Execution phase within a call frame.
/// </summary>
public enum ExecutionPhase
{
    None,
    BeforeGoal,
    ExecutingGoal,
    ExecutingStep,
    AfterGoal,
    Error
}

/// <summary>
/// Represents a single frame in the call stack.
/// Holds the Action reference — navigate the full trace on demand:
/// action.Step.Goal.Parent...
/// </summary>
public sealed class CallFrame : IAsyncDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly List<object> _disposables = new();

    /// <summary>
    /// Unique identifier for this frame.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The action being executed. Navigate the full trace on demand:
    /// action.Step.Goal.Parent...
    /// </summary>
    public App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; }

    /// <summary>
    /// Variable snapshot — variables that changed during this frame's execution.
    /// Captured in SnapshotVariables() using Data.Updated > StartedAt.
    /// </summary>
    public Dictionary<string, string>? Variables { get; private set; }

    /// <summary>
    /// Parent frame (caller).
    /// </summary>
    public CallFrame? Parent { get; }

    /// <summary>
    /// When the frame was created.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// When the frame completed (null if still executing).
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Errors that occurred during this frame's execution.
    /// </summary>
    public List<IError> Errors { get; } = new();

    /// <summary>
    /// The error being handled in this frame. Set by error.check before calling the error goal.
    /// </summary>
    public IError? Error { get; set; }

    /// <summary>
    /// Associated event (if executing within an event handler).
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Indent level for logging.
    /// </summary>
    public int Indent { get; }

    /// <summary>
    /// Current execution phase.
    /// </summary>
    public ExecutionPhase Phase { get; set; }

    public CallFrame(App.Goals.Goal.Steps.Step.Actions.Action.@this action, CallFrame? parent = null)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Action = action;
        Parent = parent;
        Phase = ExecutionPhase.None;
        StartedAt = DateTime.UtcNow;
        Indent = parent != null ? parent.Indent + 1 : 0;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration => _stopwatch.Elapsed;

    /// <summary>
    /// Snapshots variables that changed during this frame's execution.
    /// Captures Data.Updated > StartedAt from the Variables.
    /// </summary>
    public void SnapshotVariables(App.Variables.@this variables)
    {
        Variables = variables.GetChangedSince(StartedAt);
    }

    /// <summary>
    /// Marks the frame as completed.
    /// </summary>
    public void Complete()
    {
        CompletedAt = DateTime.UtcNow;
        _stopwatch.Stop();
        Phase = Errors.Count > 0 ? ExecutionPhase.Error : ExecutionPhase.None;
    }

    /// <summary>
    /// Checks if this frame is within an event handler.
    /// </summary>
    public bool IsInEvent => EventId != null || (Parent?.IsInEvent ?? false);

    /// <summary>
    /// Gets the depth of this frame in the call stack.
    /// </summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            var current = Parent;
            while (current != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }
    }

    /// <summary>
    /// Gets a stack trace string for this frame.
    /// </summary>
    public string GetStackTrace()
    {
        var goal = Action.Step?.Goal;
        var step = Action.Step;
        var trace = $"  at {goal?.Name ?? Action.Module}.{Action.ActionName}";
        if (step != null)
            trace += $" (step {step.Index + 1})";
        if (!string.IsNullOrEmpty(goal?.Path))
            trace += $" in {goal.Path}";
        trace += $" [{Duration.TotalMilliseconds:F1}ms]";
        return trace;
    }

    /// <summary>
    /// Registers a disposable object to be cleaned up when this frame exits.
    /// </summary>
    public void AddDisposable(object disposable) => _disposables.Add(disposable);

    /// <summary>
    /// Transfers a disposable to another frame (e.g., when a goal returns a disposable to its parent).
    /// </summary>
    public void TransferDisposable(object disposable, CallFrame target)
    {
        _disposables.Remove(disposable);
        target.AddDisposable(disposable);
    }

    /// <summary>
    /// Disposes all tracked objects. Called when the frame is popped from the call stack.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var d in _disposables)
        {
            if (d is IAsyncDisposable ad) await ad.DisposeAsync();
            else if (d is IDisposable sync) sync.Dispose();
        }
        _disposables.Clear();
    }

    public SerializableCallFrame ToSerializable() => new()
    {
        Id = Id,
        GoalName = Action.Step?.Goal?.Name ?? Action.Module,
        GoalPath = Action.Step?.Goal?.Path,
        Phase = Phase.ToString(),
        CurrentStepIndex = Action.Step?.Index ?? -1,
        CurrentStepText = Action.Step?.Text,
        StartedAt = StartedAt,
        Duration = Duration,
        Depth = Depth,
        HasErrors = Errors.Count > 0
    };

    public override string ToString() => $"[{Id}] {Action.Step?.Goal?.Name ?? Action.Module} - {Phase}";
}
