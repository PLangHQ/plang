using Goal = App.Goals.Goal.@this;
using Call = App.CallStack.Call.@this;

namespace App.Errors;

/// <summary>
/// Interface for all App error types.
/// </summary>
public interface IError
{
    string Id { get; }
    string Message { get; }
    string Key { get; }
    int StatusCode { get; }
    ErrorCategory Category { get; }
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
    /// The step where the error occurred.
    /// </summary>
    Step? Step { get; set; }

    /// <summary>
    /// The goal where the error occurred.
    /// </summary>
    Goal? Goal { get; set; }

    /// <summary>
    /// Snapshot of the Call chain from the failing scope upward to the root. Index <c>[0]</c>
    /// is the failing Call itself (post-Push snapshot — chain includes self).
    /// </summary>
    IReadOnlyList<Call> CallFrames { get; set; }

    /// <summary>
    /// Snapshot of variable names and their values at the time of the error.
    /// Captured from the Variables when the error is enriched in Step.RunAsync.
    /// </summary>
    Dictionary<string, string> Variables { get; set; }

    /// <summary>
    /// Formats this error for display. Called only at the final display point, never during propagation.
    /// </summary>
    string Format();
}
