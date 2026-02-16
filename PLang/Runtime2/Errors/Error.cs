using System.Text;
using PLang.Runtime2;
using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

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
    public Step? Step { get; init; }
    public IReadOnlyList<CallFrame> CallFrames { get; init; } = Array.Empty<CallFrame>();
    public virtual ErrorCategory Category => StatusCode < 500 ? ErrorCategory.Application : ErrorCategory.Runtime;

    public Error(string message, string key = "Error", int statusCode = 400)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = DateTime.UtcNow;
    }

    public Error(string message, Step step, string key = "Error", int statusCode = 400)
        : this(message, key, statusCode)
    {
        Step = step;
    }

    public Error(string message, Step step, IReadOnlyList<CallFrame> callFrames, string key = "Error", int statusCode = 400)
        : this(message, step, key, statusCode)
    {
        CallFrames = callFrames;
    }

    public Error(string message, PLangContext context, string key = "Error", int statusCode = 400)
        : this(message, context.Step!, key, statusCode)
    {
        CallFrames = context.CallStack?.GetFrames() ?? (IReadOnlyList<CallFrame>)Array.Empty<CallFrame>();
    }

    public static Error FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public static Error FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }

    public virtual string Format()
    {
        var sb = new StringBuilder();
        FormatSingle(this, sb, "");

        for (int i = 0; i < ErrorChain.Count; i++)
        {
            sb.AppendLine();
            sb.AppendLine($"--- Error during error handling [{i + 1}] ---");
            FormatSingle(ErrorChain[i], sb, "\t");
        }
        return sb.ToString().TrimEnd();
    }

    private static void FormatSingle(IError error, StringBuilder sb, string indent)
    {
        if (error.Category == ErrorCategory.Application)
        {
            FormatApplication(error, sb, indent);
        }
        else
        {
            FormatRuntime(error, sb, indent);
        }
    }

    private static void FormatApplication(IError error, StringBuilder sb, string indent)
    {
        var file = error.Step?.Goal?.Path != null ? $"{error.Step.Goal.Path}:{error.Step.LineNumber}" : null;

        sb.AppendLine($"{indent}\u26a0\ufe0f  Error({error.StatusCode}) \u2014 {error.Key}");
        sb.AppendLine($"{indent}Error: {error.Message}");

        if (error.Step != null)
            sb.AppendLine($"{indent}\ud83d\udcdd Step: - {error.Step.Text}");
        if (file != null)
            sb.AppendLine($"{indent}\ud83d\udcc4 File: {file}");

        if (error.FixSuggestion != null)
            sb.AppendLine($"{indent}\ud83d\udee0\ufe0f  Fix: {error.FixSuggestion}");

        if (error is Error e)
            e.FormatExtra(sb, indent);
    }

    private static void FormatRuntime(IError error, StringBuilder sb, string indent)
    {
        var file = error.Step?.Goal?.Path != null ? $"{error.Step.Goal.Path}:{error.Step.LineNumber}" : null;
        var line = error.Step?.LineNumber;

        sb.AppendLine($"{indent}\ud83d\udd34   ================== {error.Key}({error.StatusCode}) ==================   \ud83d\udd34");
        if (file != null)
            sb.AppendLine($"{indent}\ud83d\udcc4 File: {file}");
        if (line != null)
            sb.AppendLine($"{indent}\ud83d\udd22 Line: {line}");
        sb.AppendLine($"{indent}\ud83e\udde9 Key:  {error.Key}");
        sb.AppendLine($"{indent}#\ufe0f\u20e3  StatusCode:  {error.StatusCode}");
        sb.AppendLine($"{indent}\ud83d\udd51 Time: {error.CreatedUtc}");

        // Error Details
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

        sb.AppendLine();
        sb.AppendLine($"{indent}\ud83e\uddd0 Reason: ");
        sb.AppendLine($"{indent}    {error.Message}");

        if (error.FixSuggestion != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udee0\ufe0f  Fix Suggestions:");
            sb.AppendLine($"{indent}    {error.FixSuggestion}");
        }

        if (error.HelpfulLinks != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udd17 Helpful Links:");
            sb.AppendLine($"{indent}    {error.HelpfulLinks}");
        }

        if (error.CallFrames.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\ud83d\udedd  Call stack:");
            foreach (var frame in error.CallFrames)
            {
                var stepInfo = frame.Step != null ? $":{frame.Step.LineNumber}" : "";
                sb.AppendLine($"{indent}    {frame.GoalName} - {frame.GoalPath}{stepInfo}");
            }
        }

        if (error is Error e)
            e.FormatExtra(sb, indent);

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
