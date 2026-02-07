using System.Collections.Concurrent;
using System.Text;
using PLang.Runtime2.Errors;

namespace PLang.Runtime2.Core;

/// <summary>
/// Tracks the call stack during PLang execution.
/// Thread-safe and optional (can be disabled for performance).
/// </summary>
public sealed class CallStack
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
    public CallFrame Push(string goalName, string? goalPath = null)
    {
        if (!IsEnabled)
            return new CallFrame(goalName, goalPath);

        if (_frames.Count >= MaxDepth)
            throw new CallStackOverflowException(MaxDepth);

        var parent = Current;
        var frame = new CallFrame(goalName, goalPath, parent);
        _frames.Push(frame);
        return frame;
    }

    /// <summary>
    /// Pops the current frame from the call stack.
    /// </summary>
    public CallFrame? Pop()
    {
        if (!IsEnabled)
            return null;

        if (_frames.TryPop(out var frame))
        {
            frame.Complete();
            return frame;
        }
        return null;
    }

    /// <summary>
    /// Peeks at the current frame without removing it.
    /// </summary>
    public CallFrame? Peek() => Current;

    /// <summary>
    /// Records a step execution in the current frame.
    /// </summary>
    public void RecordStep(int index, string text)
    {
        if (!IsEnabled)
            return;

        var frame = Current;
        if (frame != null)
        {
            frame.CurrentStepIndex = index;
            frame.CurrentStepText = text;
            frame.RecordStep(index, text);
        }
    }

    /// <summary>
    /// Adds an error to the current frame and global error list.
    /// </summary>
    public void AddError(IError error)
    {
        lock (_errorLock)
        {
            _errors.Add(error);
        }

        if (IsEnabled)
        {
            Current?.AddError(error);
        }
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
        return _frames.Any(f => f.GoalName.Equals(goalName, StringComparison.OrdinalIgnoreCase));
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
            Frames = _frames.Select(f => new SerializableCallFrame
            {
                Id = f.Id,
                GoalName = f.GoalName,
                GoalPath = f.GoalPath,
                Phase = f.Phase.ToString(),
                CurrentStepIndex = f.CurrentStepIndex,
                CurrentStepText = f.CurrentStepText,
                StartedAt = f.StartedAt,
                Duration = f.Duration,
                Depth = f.Depth,
                HasErrors = f.Errors.Count > 0
            }).ToList(),
            Depth = Depth,
            StackTrace = GetStackTrace()
        };
    }
}

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
