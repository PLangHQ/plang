using System.Diagnostics;
using PLang.Runtime2.Engine.Errors;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.Engine.CallStack;

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
/// Tracks the execution of a goal and its steps.
/// </summary>
public sealed class CallFrame : IAsyncDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly List<ExecutedStep> _executedSteps = new();
    private readonly List<object> _disposables = new();

    /// <summary>
    /// Unique identifier for this frame.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The goal being executed. Stored as object reference (OBP rule #3).
    /// </summary>
    public Goal Goal { get; }

    /// <summary>
    /// Current execution phase.
    /// </summary>
    public ExecutionPhase Phase { get; set; }

    /// <summary>
    /// The step currently being executed in this frame.
    /// </summary>
    public Step? Step { get; set; }

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
    /// Errors that occurred during execution.
    /// </summary>
    public List<IError> Errors { get; } = new();

    /// <summary>
    /// Associated event (if executing within an event handler).
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Indent level for logging.
    /// </summary>
    public int Indent { get; }

    public CallFrame(Goal goal, CallFrame? parent = null)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Goal = goal;
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
    /// Records that a step has been executed.
    /// </summary>
    public void RecordStep(Step step)
    {
        if (_executedSteps.Count >= MaxStepsPerFrame)
            return;

        _executedSteps.Add(new ExecutedStep(step));
    }

    /// <summary>
    /// Marks the current step as completed.
    /// </summary>
    public void CompleteCurrentStep(TimeSpan? duration = null)
    {
        if (_executedSteps.Count == 0) return;

        var last = _executedSteps[^1];
        last.CompletedAt = DateTime.UtcNow;
        last.Duration = duration ?? (last.CompletedAt.Value - last.StartedAt);
    }

    /// <summary>
    /// Gets all executed steps in this frame.
    /// </summary>
    public IReadOnlyList<ExecutedStep> ExecutedSteps => _executedSteps;

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
    /// Adds an error to this frame.
    /// </summary>
    public void AddError(IError error)
    {
        Errors.Add(error);
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
        var trace = $"  at {Goal.Name}";
        if (Step != null)
            trace += $" (step {Step.Index + 1})";
        if (!string.IsNullOrEmpty(Goal.Path))
            trace += $" in {Goal.Path}";
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
        GoalName = Goal.Name,
        GoalPath = Goal.Path,
        Phase = Phase.ToString(),
        CurrentStepIndex = Step?.Index ?? -1,
        CurrentStepText = Step?.Text,
        StartedAt = StartedAt,
        Duration = Duration,
        Depth = Depth,
        HasErrors = Errors.Count > 0
    };

    public override string ToString() => $"[{Id}] {Goal.Name} - {Phase}";

    public const int MaxStepsPerFrame = 100_000;
}
