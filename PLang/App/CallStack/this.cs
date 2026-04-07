using System.Collections.Concurrent;
using System.Text;
using App.Errors;
using Goal = App.Goals.Goal.@this;

namespace App.CallStack;

/// <summary>
/// Tracks the call stack during PLang execution.
/// Optional (can be disabled for performance). When disabled, frames are only
/// created on error — zero overhead on the happy path.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentStack<CallFrame> _frames = new();

    /// <summary>
    /// Maximum depth allowed (prevents infinite recursion).
    /// </summary>
    public int MaxDepth { get; set; } = 1000;

    /// <summary>
    /// Whether tracking is enabled during normal execution.
    /// When disabled, error frames are still created on demand.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// All errors that occurred during execution. Inspect at end of run
    /// for a complete error history, even if errors were handled.
    /// </summary>
    public List<IError> Errors { get; } = new();

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
    public CallFrame Push(App.Goals.Goal.Steps.Step.Actions.Action.@this action)
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
    /// Creates an error frame on demand. Used when IsEnabled=false and an error occurs.
    /// Captures the action and pushes a frame so the error trace is available.
    /// </summary>
    public CallFrame PushError(App.Goals.Goal.Steps.Step.Actions.Action.@this action, IError error,
        App.Variables.@this? variables = null)
    {
        var parent = Current;
        var frame = new CallFrame(action, parent);
        frame.Error = error;
        frame.Errors.Add(error);
        if (variables != null) frame.SnapshotVariables(variables);
        _frames.Push(frame);
        Errors.Add(error);
        return frame;
    }

    /// <summary>
    /// Pops the current frame from the call stack and disposes its tracked objects.
    /// </summary>
    public async Task<CallFrame?> PopAsync()
    {
        if (!_frames.TryPop(out var frame))
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
        if (_frames.IsEmpty)
            return "(no stack trace available)";

        var sb = new StringBuilder();
        foreach (var frame in _frames)
        {
            sb.AppendLine(frame.GetStackTrace());
        }
        return sb.ToString().TrimEnd();
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
    /// Clears the call stack and error history.
    /// </summary>
    public void Clear()
    {
        _frames.Clear();
        Errors.Clear();
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
