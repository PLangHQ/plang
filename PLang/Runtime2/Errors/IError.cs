namespace PLang.Runtime2.Errors;

/// <summary>
/// Interface for all Runtime2 error types.
/// </summary>
public interface IError
{
    string Id { get; }
    string Message { get; }
    string Key { get; }
    int StatusCode { get; }
    string? FixSuggestion { get; }
    string? HelpfulLinks { get; }
    DateTime CreatedUtc { get; }
    Exception? Exception { get; }

    /// <summary>
    /// Chain of errors that occurred during error handling.
    /// Original error stays as root, subsequent errors are appended.
    /// </summary>
    List<IError> ErrorChain { get; }

    /// <summary>
    /// The step where the error occurred. Navigate to goal, line number, etc. via Step.Goal.
    /// </summary>
    Core.Step? Step { get; }

    /// <summary>
    /// Snapshot of the call stack frames at the time the error was created.
    /// Reads bottom-up: first frame is the root goal, last frame is where the error occurred.
    /// </summary>
    IReadOnlyList<Core.CallFrame> CallFrames { get; }

    /// <summary>
    /// Formats this error for display. Called only at the final display point, never during propagation.
    /// </summary>
    string Format();
}
