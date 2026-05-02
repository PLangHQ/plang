using System.Text;
using App.Actor.Context;
using Goal = App.Goals.Goal.@this;
using Call = App.CallStack.Call.@this;

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
    /// <summary>
    /// Arbitrary structured context about the failure. Providers attach things
    /// like raw LLM responses, HTTP status bodies, parse positions, etc. Exposed
    /// to PLang goals via %!error.Details.KEY% so error handlers can report
    /// rich context without the provider having to extend the error class.
    /// </summary>
    public Dictionary<string, object?>? Details { get; set; }
    /// <summary>
    /// Snapshot of the parameters as they arrived at the failing handler — the .pr
    /// raw value/type and the resolved final value for each. Populated by the
    /// source-generated ExecuteAsync whenever a handler returns an error. Lets you
    /// see "this is what the handler saw" without re-running with a debug flag.
    /// </summary>
    public List<ParamSnapshot>? Params { get; set; }
    public List<IError> ErrorChain { get; } = new();
    public Step? Step { get; set; }
    public Goal? Goal { get; set; }
    public IReadOnlyList<Call> CallFrames { get; set; } = Array.Empty<Call>();
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// The execution context where this error occurred. Used by verbose debug to dump variables.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Actor.Context.@this? Context { get; set; }

    public virtual ErrorCategory Category => StatusCode < 500 ? ErrorCategory.Application : ErrorCategory.Runtime;

    /// <summary>
    /// Creates an error with a message. Use for errors not tied to a specific execution context.
    /// </summary>
    public Error(string message, string key = "Error", int statusCode = 400)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Message = message;
        Key = key;
        StatusCode = statusCode;
        CreatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates an error tied to a specific step. Goal is inferred from step.Goal.
    /// </summary>
    public Error(string message, Step? step, string key = "Error", int statusCode = 400)
        : this(message, key, statusCode)
    {
        Step = step;
        Goal = step?.Goal;
    }

    /// <summary>
    /// Creates an error with a step and explicit Call chain snapshot.
    /// </summary>
    public Error(string message, Step? step, IReadOnlyList<Call> callFrames, string key = "Error", int statusCode = 400)
        : this(message, step, key, statusCode)
    {
        CallFrames = callFrames;
    }

    /// <summary>
    /// Creates an error from an execution context. Captures step, goal, and Call chain automatically.
    /// </summary>
    public Error(string message, Actor.Context.@this context, string key = "Error", int statusCode = 400)
        : this(message, context.Step, key, statusCode)
    {
        Goal = context.Goal;
        Context = context;
        CallFrames = context.CallStack?.Current?.SnapshotChain() ?? (IReadOnlyList<Call>)Array.Empty<Call>();
    }

    /// <summary>
    /// Wraps a CLR exception as an Error. StatusCode defaults to 500 (runtime error).
    /// </summary>
    public static Error FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new Error(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    /// <summary>
    /// Wraps a CLR exception as an Error with execution context for step/goal/callstack capture.
    /// </summary>
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

        // Per-parameter snapshot — what the handler actually saw at dispatch.
        // The source-generated ExecuteAsync captures this on every error path so the
        // reader doesn't have to re-run with a debug flag to find out which param
        // was wrong and how. Showing PrValue and FinalValue side-by-side reveals
        // resolution failures at a glance (PrValue="%messages%", FinalValue=null
        // → variable lookup returned nothing).
        if (error is Error errWithParams && errWithParams.Params is { Count: > 0 } parms)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}📥 Parameters at dispatch:");
            foreach (var p in parms)
            {
                var declared = p.DeclaredType != null ? $" ({p.DeclaredType})" : "";
                sb.AppendLine($"{indent}    {p.Name}{declared}");
                var pr = FormatVerboseValue(p.PrValue);
                var prType = p.PrType != null ? $" [{p.PrType}]" : "";
                sb.AppendLine($"{indent}        .pr value:  {pr}{prType}");
                if (p.WasAccessed)
                {
                    var final = FormatVerboseValue(p.FinalValue);
                    sb.AppendLine($"{indent}        final:      {final}");
                }
                else
                {
                    sb.AppendLine($"{indent}        final:      (not accessed)");
                }
            }
        }

        // Verbose variable dump — shows all variables in scope at point of failure
        var app = error.Goal?.App ?? error.Step?.Goal?.App;
        if (app?.Debug?.Verbose == true)
        {
            var errorContext = (error as Error)?.Context;
            var fallbackContext = app.Context;
            var ctx = errorContext ?? fallbackContext;
            var allVars = ctx?.Variables?.GetAll();
            if (allVars != null)
            {
                var ctxId = ctx?.Id ?? "?";
                var ctxSource = errorContext != null ? "error context" : "app context (error context not captured)";
                sb.AppendLine();
                sb.AppendLine($"{indent}📋 Variables in scope ({ctxSource}, id={ctxId}):");
                foreach (var kvp in allVars)
                {
                    var val = FormatVerboseValue(kvp.Value.Value);
                    sb.AppendLine($"{indent}    %{kvp.Key}% = {val} ({kvp.Value.Type?.Value ?? "?"})");
                }
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

    private static string FormatVerboseValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s)
            return s.Length > 200 ? $"\"{s[..200]}...\" ({s.Length} chars)" : $"\"{s}\"";
        if (value is System.Collections.IDictionary or System.Collections.IList)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return json.Length > 300 ? json[..300] + $"... ({json.Length} chars)" : json;
            }
            catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException) { return value.ToString() ?? "?"; }
        }
        var str = value.ToString() ?? "?";
        return str.Length > 200 ? $"{str[..200]}... ({str.Length} chars)" : str;
    }

    protected virtual void FormatExtra(StringBuilder sb, string indent) { }

    public override string ToString() => $"[{Key}] {Message}";
}
