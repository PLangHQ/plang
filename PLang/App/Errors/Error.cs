using System.Text;
using App.Actor.Context;
using Goal = App.Goals.Goal.@this;

namespace App.Errors;

/// <summary>
/// Generic base error class implementing IError.
/// Captures execution context (step, goal) when provided.
/// </summary>
public class Error : IError
{
    public string Id { get; }
    public string Message { get; }
    public string Key { get; }
    public int StatusCode { get; }
    public string? FixSuggestion { get; init; }
    public string? HelpfulLinks { get; init; }
    public DateTime CreatedUtc { get; }
    public Exception? Exception { get; init; }
    public List<IError> ErrorChain { get; } = new();
    public Step? Step { get; set; }
    public Goal? Goal { get; set; }
    public IReadOnlyList<CallFrame> CallFrames { get; set; } = Array.Empty<CallFrame>();
    public Dictionary<string, string> Variables { get; set; } = new();
    public virtual ErrorCategory Category => StatusCode < 500 ? ErrorCategory.Application : ErrorCategory.Runtime;

    public Error(string message, string key = "Error", int statusCode = 400)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = DateTime.UtcNow;
    }

    public Error(string message, Step? step, string key = "Error", int statusCode = 400)
        : this(message, key, statusCode)
    {
        Step = step;
        Goal = step?.Goal;
    }

    public Error(string message, Step? step, IReadOnlyList<CallFrame> callFrames, string key = "Error", int statusCode = 400)
        : this(message, step, key, statusCode)
    {
        CallFrames = callFrames;
    }

    public Error(string message, Actor.Context.@this context, string key = "Error", int statusCode = 400)
        : this(message, context.Step, key, statusCode)
    {
        Goal = context.Goal;
        CallFrames = context.CallStack?.GetFrames() ?? (IReadOnlyList<CallFrame>)Array.Empty<CallFrame>();
    }

    public static Error FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public static Error FromException(Exception ex, Actor.Context.@this context, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }

    public virtual string Format()
    {
        var sb = new StringBuilder();
        FormatError(this, sb, "");

        for (int i = 0; i < ErrorChain.Count; i++)
        {
            sb.AppendLine();
            sb.AppendLine($"--- Error during error handling [{i + 1}] ---");
            FormatError(ErrorChain[i], sb, "\t");
        }
        return sb.ToString().TrimEnd();
    }

    private static void FormatError(IError error, StringBuilder sb, string indent)
    {
        var goalPath = error.Goal?.Path ?? error.Step?.Goal?.Path;
        var file = goalPath != null && error.Step != null ? $"{goalPath}:{error.Step.LineNumber}" : goalPath;

        // Header
        sb.AppendLine($"{indent}\ud83d\udd34   ================== {error.Key}({error.StatusCode}) ==================   \ud83d\udd34");
        if (file != null)
            sb.AppendLine($"{indent}\ud83d\udcc4 File: {file}");
        if (error.Step != null)
            sb.AppendLine($"{indent}\ud83d\udd22 Line: {error.Step.LineNumber}");
        sb.AppendLine($"{indent}\ud83e\udde9 Key:  {error.Key}");
        sb.AppendLine($"{indent}#\ufe0f\u20e3  StatusCode:  {error.StatusCode}");
        sb.AppendLine($"{indent}\ud83d\udd51 Time: {error.CreatedUtc}");

        // Code snippet
        sb.AppendLine();
        sb.AppendLine($"{indent}\ud83d\udd0d   ================== Error Details ==================   \ud83d\udd0d");

        if (error.Step != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udcdc Code snippet that the error occurred:");
            sb.AppendLine($"{indent}    - {error.Step.Text}");
            if (file != null)
                sb.AppendLine($"{indent}        at {file}");
        }

        // Reason
        sb.AppendLine();
        sb.AppendLine($"{indent}\ud83e\uddd0 Reason: ");
        sb.AppendLine($"{indent}    {error.Message}");

        // Fix suggestions
        if (error.FixSuggestion != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udee0\ufe0f  Fix Suggestions:");
            sb.AppendLine($"{indent}    {error.FixSuggestion}");
        }

        // Helpful links
        if (error.HelpfulLinks != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udd17 Helpful Links:");
            sb.AppendLine($"{indent}    {error.HelpfulLinks}");
        }

        // Variables snapshot
        if (error.Variables.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83c\udff7\ufe0f  Variables in step:");
            foreach (var (name, value) in error.Variables)
            {
                sb.AppendLine($"{indent}    %{name}% = {value}");
            }
        }

        // Call stack
        if (error.CallFrames.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udedd  Call stack:");
            foreach (var frame in error.CallFrames)
            {
                var fStep = frame.Action.Step;
                var fGoal = fStep?.Goal;
                var stepInfo = fStep != null ? $":{fStep.LineNumber}" : "";
                sb.AppendLine($"{indent}    {fGoal?.Name ?? frame.Action.Module} - {fGoal?.Path}{stepInfo}");
            }
        }

        // Error source (ActionError overrides FormatExtra)
        if (error is Error e)
            e.FormatExtra(sb, indent);

        // Exception details
        if (error.Exception != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udc68\u200d\ud83d\udcbb For C# Developers:");
            var ex = error.Exception;
            while (ex != null)
            {
                sb.AppendLine($"{indent}    - {ex.GetType().Name}: {ex.Message}");
                if (ex.StackTrace != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{indent}    StackTrace: {ex.StackTrace}");
                }
                ex = ex.InnerException;
                if (ex != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{indent}    Inner Exception:");
                }
            }
        }
    }

    protected virtual void FormatExtra(StringBuilder sb, string indent) { }

    public override string ToString() => $"[{Key}] {Message}";
}
