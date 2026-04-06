using System.Collections.Concurrent;
using System.Text;
using App.Engine.Errors;
using Goal = App.Engine.Goals.Goal.@this;

namespace App.Engine.CallStack;

/// <summary>
/// Tracks the call stack during PLang execution.
/// Thread-safe and optional (can be disabled for performance).
/// </summary>
public sealed class @this
{
    private readonly ConcurrentStack<CallFrame> _frames = new();
    private readonly List<IError> _errors = new();
    private readonly object _errorLock = new();

    /// <summary>
    /// Maximum depth allowed (prevents infinite recursion).
    /// </summary>
    public int MaxDepth { get; set; } = 1000;

    /// <summary>
    /// Whether tracking is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Current depth of the call stack.
    /// </summary>
    public int Depth => _frames.Count;

    /// <summary>
    /// Gets the current (top) frame.
    /// </summary>
    public CallFrame? Current => _frames.TryPeek(out var frame) ? frame : null;

    /// <summary>
    /// Pushes a new frame onto the call stack.
    /// </summary>
    public CallFrame Push(App.Engine.Goals.Goal.Steps.Step.Actions.Action.@this action)
    {
        if (!IsEnabled)
            return new CallFrame(action);

        if (_frames.Count >= MaxDepth)
            throw new CallStackOverflowException(MaxDepth);

        var parent = Current;
        var frame = new CallFrame(action, parent);
        _frames.Push(frame);
        return frame;
    }

    /// <summary>
    /// Pops the current frame from the call stack and disposes its tracked objects.
    /// </summary>
    public async Task<CallFrame?> PopAsync()
    {
        if (!IsEnabled || !_frames.TryPop(out var frame))
            return null;

        frame.Complete();
        await frame.DisposeAsync();
        return frame;
    }

    /// <summary>
    /// Peeks at the current frame without removing it.
    /// </summary>
    public CallFrame? Peek() => Current;

    /// <summary>
    /// Records a step execution in the current frame.
    /// </summary>
    public void RecordStep(Step step)
    {
        if (!IsEnabled) return;

        var frame = Current;
        if (frame == null) return;

        frame.RecordStep(step);
    }

    /// <summary>
    /// Adds an error to the current frame and global error list.
    /// </summary>
    public void AddError(IError error)
    {
        lock (_errorLock) { _errors.Add(error); }

        if (!IsEnabled) return;

        Current?.AddError(error);
    }

    /// <summary>
    /// Gets all errors that have occurred.
    /// </summary>
    public IReadOnlyList<IError> GetErrors()
    {
        lock (_errorLock)
        {
            return _errors.ToList();
        }
    }

    /// <summary>
    /// Clears all errors.
    /// </summary>
    public void ClearErrors()
    {
        lock (_errorLock)
        {
            _errors.Clear();
        }
    }

    /// <summary>
    /// Gets all frames in the call stack (from top to bottom).
    /// </summary>
    public IReadOnlyList<CallFrame> GetFrames()
    {
        return _frames.ToList();
    }

    /// <summary>
    /// Gets a formatted stack trace string.
    /// </summary>
    public string GetStackTrace()
    {
        if (!IsEnabled || _frames.IsEmpty)
            return "(no stack trace available)";

        var sb = new StringBuilder();
        foreach (var frame in _frames)
        {
            sb.AppendLine(frame.GetStackTrace());
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Gets the flat execution history across all frames.
    /// </summary>
    public IEnumerable<(CallFrame Frame, ExecutedStep Step)> GetExecutionHistory()
    {
        foreach (var frame in _frames.Reverse())
        {
            foreach (var step in frame.ExecutedSteps)
            {
                yield return (frame, step);
            }
        }
    }

    /// <summary>
    /// Checks if currently executing within an event handler.
    /// </summary>
    public bool IsInEvent => Current?.IsInEvent ?? false;

    /// <summary>
    /// Checks if a specific goal is already in the call stack.
    /// </summary>
    public bool ContainsGoal(string goalName)
    {
        return _frames.Any(f => (f.Action.Step?.Goal?.Name ?? f.Action.Module).Equals(goalName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears the call stack.
    /// </summary>
    public void Clear()
    {
        _frames.Clear();
        ClearErrors();
    }

    /// <summary>
    /// Converts to a serializable representation.
    /// </summary>
    public SerializableCallStack ToSerializable()
    {
        return new SerializableCallStack
        {
            Frames = _frames.Select(f => f.ToSerializable()).ToList(),
            Depth = Depth,
            StackTrace = GetStackTrace()
        };
    }
}
