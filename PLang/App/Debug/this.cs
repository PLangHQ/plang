using App.Variables;

using System.Text;
using System.Text.RegularExpressions;
using App.Actor.Context;
using App.Events;
using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

namespace App.Debug;

/// <summary>
/// Provides debug output for PLang execution when !debug is passed on the command line.
/// Registers events to dump step info, call stack, and memory stack to stderr.
/// </summary>
public sealed class @this
{
    private readonly App.@this _engine;

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Filter to a specific goal name. Null = all goals.
    /// </summary>
    public string? Goal { get; set; }

    /// <summary>
    /// Filter to a specific step index. Null = all steps.
    /// </summary>
    public int? Step { get; set; }

    /// <summary>
    /// Variables to watch. Each can optionally track events (OnCreate/OnChange/OnDelete).
    /// Variables without Event set are displayed at step boundaries.
    /// Set via: --debug={"variables":[{"name":"trace","event":"onchange"}]}
    /// </summary>
    public List<DebugVariable>? Variables { get; set; }

    /// <summary>
    /// Max characters per line before truncation. Default 500.
    /// </summary>
    public int MaxLength { get; set; } = 500;

    /// <summary>
    /// Regex string to filter debug output lines.
    /// </summary>
    public string? Grep { get; set; }

    /// <summary>
    /// Debug detail level. "step" (default) or "action" (shows state between actions).
    /// </summary>
    public string Level { get; set; } = "step";

    /// <summary>
    /// When true, errors include a dump of all available variables at the point of failure.
    /// Useful for diagnosing missing variables in foreach/goal.call chains.
    /// Set via: --debug={"verbose":true}
    /// </summary>
    public bool Verbose { get; set; }


    [System.Text.Json.Serialization.JsonIgnore]
    private Regex? _grepRegex;

    public @this(App.@this engine)
    {
        _engine = engine;
    }

    public void Apply(object debugValue)
    {
        IsEnabled = true;

        if (debugValue is IDictionary<string, object?> dict)
            App.Utils.TypeMapping.Populate(this, dict);

        // Strip % from variable names
        if (Variables != null)
        {
            foreach (var v in Variables)
                v.Name = v.Name.Trim('%');

            // Create placeholder Data with event handlers for watched variables
            var vars = _engine.User.Context.Variables;
            foreach (var v in Variables.Where(v => v.Event.HasValue))
            {
                var placeholder = Data.@this.Uninitialized(v.Name);
                if (v.Event == DebugEvent.OnCreate)
                    placeholder.OnCreate += (data) => LogEvent(v.Name, "CREATED", data);
                if (v.Event == DebugEvent.OnChange)
                    placeholder.OnChange += (oldData, newData) => LogMutation(v.Name, oldData, newData);
                if (v.Event == DebugEvent.OnDelete)
                    placeholder.OnDelete += (data) => LogEvent(v.Name, "DELETED", data);
                vars.Put(placeholder);
            }
        }

        // Build grep regex
        if (!string.IsNullOrEmpty(Grep))
        {
            try { _grepRegex = new Regex(Grep, RegexOptions.IgnoreCase); }
            catch { _grepRegex = new Regex(Regex.Escape(Grep), RegexOptions.IgnoreCase); }
        }

        var events = _engine.Context.Events;

        events.Register(new EventBinding(
            EventType.BeforeStep,
            context => BeforeStepHandler(context, Step),
            goalNamePattern: Goal ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        events.Register(new EventBinding(
            EventType.AfterStep,
            context => AfterStepHandler(context, Step),
            goalNamePattern: Goal ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        events.Register(new EventBinding(
            EventType.AfterGoal,
            AfterGoalHandler,
            goalNamePattern: Goal ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        if (string.Equals(Level, "action", StringComparison.OrdinalIgnoreCase))
        {
            events.Register(new EventBinding(
                EventType.BeforeAction,
                context => BeforeActionHandler(context, Step),
                goalNamePattern: Goal ?? "*",
                priority: int.MaxValue,
                stopOnError: false));

            events.Register(new EventBinding(
                EventType.AfterAction,
                context => AfterActionHandler(context, Step),
                goalNamePattern: Goal ?? "*",
                priority: int.MaxValue,
                stopOnError: false));
        }
    }


    public void LogMutation(string name, Data.@this oldData, Data.@this newData)
    {
        var context = _engine.User.Context;
        var goalName = context?.Goal?.Name ?? "?";
        var stepIndex = context?.Step?.Index.ToString() ?? "?";
        var stepText = context?.Step?.Text;
        if (stepText != null && stepText.Length > 60) stepText = stepText[..60];
        var stack = new System.Diagnostics.StackTrace(2, true);

        var sb = new StringBuilder();
        sb.AppendLine($"=== WATCH [{name}] CHANGED ===");
        sb.AppendLine($"  Goal: {goalName}[{stepIndex}] {stepText ?? "?"}");
        sb.AppendLine($"  Raw: {oldData.RawValue?.GetType().Name ?? "null"} → {newData.RawValue?.GetType().Name ?? "null"}");
        sb.AppendLine($"  Resolved: {oldData.Value?.GetType().Name ?? "null"} → {newData.Value?.GetType().Name ?? "null"}");
        sb.AppendLine($"  NeedsRes: {newData.NeedsResolution}, HasCtx: {newData.Context != null}");
        for (int i = 0; i < Math.Min(5, stack.FrameCount); i++)
        {
            var frame = stack.GetFrame(i);
            if (frame?.GetMethod() != null)
                sb.AppendLine($"  at {frame.GetMethod()!.DeclaringType?.Name}.{frame.GetMethod()!.Name}:{frame.GetFileLineNumber()}");
        }
        sb.AppendLine("==============================");
        Console.Error.Write(sb.ToString());
    }

    public void LogEvent(string name, string eventType, Data.@this data)
    {
        var context = _engine.User.Context;
        var goalName = context?.Goal?.Name ?? "?";
        var stepIndex = context?.Step?.Index.ToString() ?? "?";

        Console.Error.WriteLine($"=== WATCH [{name}] {eventType} in {goalName}[{stepIndex}] type={data.Value?.GetType().Name ?? "null"} ===");
    }

    private static Task<Data.@this> BeforeStepHandler(Actor.Context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(App.Data.@this.Ok());
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return Task.FromResult(App.Data.@this.Ok());

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [BEFORE]: Step [{step.Index}] of {goalName} ===");
        sb.AppendLine($"  Text: {step.Text}");

        foreach (var action in step.Actions)
        {
            sb.AppendLine($"  Action: {action.Module}.{action.ActionName}");
            foreach (var p in action.Parameters)
            {
                sb.AppendLine($"    {p.Name} = {FormatValue(p.Value, context)}");
            }

            if (action.Return != null && action.Return.Count > 0)
            {
                var returnStr = string.Join(", ", action.Return.Select(r => $"%{r.Name}%"));
                sb.AppendLine($"  Return: {returnStr}");
            }
        }

        var callStack = context.CallStack;
        if (callStack != null)
        {
            sb.AppendLine("  Call Stack:");
            foreach (var line in callStack.GetStackTrace().Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine($"    {line.TrimEnd()}");
            }
        }

        AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        WriteFiltered(sb, context);
        return Task.FromResult(App.Data.@this.Ok());
    }

    private static Task<Data.@this> AfterStepHandler(Actor.Context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(App.Data.@this.Ok());
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return Task.FromResult(App.Data.@this.Ok());

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [AFTER]: Step [{step.Index}] of {goalName} ===");

        AppendStepVariables(sb, context);
        sb.AppendLine("========================================");

        WriteFiltered(sb, context);
        return Task.FromResult(App.Data.@this.Ok());
    }

    private static void WriteFiltered(StringBuilder sb, Actor.Context.@this context)
    {
        var debug = context.App?.Debug;
        var maxLen = debug?.MaxLength ?? 500;
        var grep = debug?._grepRegex;
        var output = sb.ToString();

        // Grep first on full content
        if (grep != null)
        {
            var filtered = new StringBuilder();
            foreach (var line in output.Split('\n'))
            {
                if (grep.IsMatch(line))
                    filtered.AppendLine(line);
            }
            output = filtered.ToString();
        }

        // Then truncate lines for display
        if (maxLen > 0)
        {
            var truncated = new StringBuilder();
            foreach (var line in output.Split('\n'))
            {
                truncated.AppendLine(line.Length > maxLen
                    ? $"{line[..maxLen]}... ({line.Length} chars)"
                    : line);
            }
            output = truncated.ToString();
        }

        Console.Error.Write(output);
    }

    private static Task<Data.@this> AfterGoalHandler(Actor.Context.@this context)
    {
        var goalName = context.Goal?.Name ?? "?";
        Console.Error.WriteLine($"--- DEBUG: Goal '{goalName}' completed ---");
        return Task.FromResult(App.Data.@this.Ok());
    }

    private static Task<Data.@this> BeforeActionHandler(Actor.Context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(App.Data.@this.Ok());
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return Task.FromResult(App.Data.@this.Ok());

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [BEFORE] in Step [{step.Index}] of {goalName} ---");

        AppendStepVariables(sb, context);

        WriteFiltered(sb, context);
        return Task.FromResult(App.Data.@this.Ok());
    }

    private static Task<Data.@this> AfterActionHandler(Actor.Context.@this context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(App.Data.@this.Ok());
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return Task.FromResult(App.Data.@this.Ok());

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();
        sb.AppendLine($"  --- ACTION [AFTER] in Step [{step.Index}] of {goalName} ---");

        AppendStepVariables(sb, context);

        WriteFiltered(sb, context);
        return Task.FromResult(App.Data.@this.Ok());
    }

    private static readonly Regex VarRefPattern = new(@"%([^%]+)%", RegexOptions.Compiled);

    private static void AppendStepVariables(StringBuilder sb, Actor.Context.@this context)
    {
        var step = context.Step;
        if (step == null) return;

        var varNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in step.Actions)
        {
            if (action.Return != null)
            {
                foreach (var r in action.Return)
                {
                    if (!string.IsNullOrEmpty(r.Name))
                        varNames.Add(r.Name);
                }
            }

            foreach (var p in action.Parameters)
            {
                if (p.Value is string s)
                {
                    foreach (Match m in VarRefPattern.Matches(s))
                        varNames.Add(m.Groups[1].Value);
                }
            }
        }

        // Add explicitly watched variables
        var watchVars = context.App?.Debug.Variables;
        if (watchVars != null)
            foreach (var v in watchVars)
                varNames.Add(v.Name);

        if (varNames.Count == 0) return;

        sb.AppendLine($"  Variables ({varNames.Count}):");
        foreach (var name in varNames)
        {
            var data = context.Variables.Get(name);
            if (data == null || !data.IsInitialized)
            {
                sb.AppendLine($"    %{name}% = (undefined)");
                continue;
            }

            sb.AppendLine($"    %{name}% = {FormatValue(data.Value, context)} ({data.Type?.Value ?? "?"})");

            if (data.Properties.Count > 0)
            {
                sb.AppendLine($"      Properties ({data.Properties.Count}):");
                foreach (var prop in data.Properties)
                {
                    sb.AppendLine($"        {prop.Name} = {FormatValue(prop.Value, context)}");
                }
            }
        }
    }

    private static string FormatValue(object? value, Actor.Context.@this context)
    {
        // Always format full content — truncation happens at WriteFiltered
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        if (value is System.Collections.IDictionary dict)
        {
            var preview = new List<string>();
            var i = 0;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (i++ >= 3) { preview.Add("..."); break; }
                var val = FormatPreviewValue(entry.Value);
                preview.Add($"{entry.Key}: {val}");
            }
            return $"{{ {string.Join(", ", preview)} }} ({dict.Count} keys)";
        }
        if (value is System.Collections.IList list)
        {
            if (list.Count == 0) return "[0 items]";
            var first = FormatPreviewValue(list[0]);
            return list.Count == 1 ? $"[1 item: {first}]" : $"[{list.Count} items, first: {first}]";
        }
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            int count = 0;
            object? first = null;
            foreach (var item in enumerable) { if (count == 0) first = item; count++; }
            if (count == 0) return "[0 items]";
            var firstStr = FormatPreviewValue(first);
            return count == 1 ? $"[1 item: {firstStr}]" : $"[{count} items, first: {firstStr}]";
        }
        var str = value.ToString() ?? "(null)";
        return str;
    }

    private static string FormatPreviewValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return s.Length > 80 ? $"\"{s[..80]}...\" ({s.Length}c)" : $"\"{s}\"";
        if (value is System.Collections.IDictionary dict)
        {
            var parts = new List<string>();
            var i = 0;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (i++ >= 4) { parts.Add("..."); break; }
                parts.Add($"{entry.Key}={TruncateToString(entry.Value, 40)}");
            }
            return $"{{ {string.Join(", ", parts)} }}";
        }
        if (value is System.Collections.ICollection col)
            return $"[{col.Count} items]";

        // For objects: show public property names and short values
        var type = value.GetType();
        if (!type.IsPrimitive && type != typeof(decimal) && type != typeof(DateTime)
            && type != typeof(Guid) && !type.IsEnum)
        {
            var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.Name != "EqualityContract")
                .Take(5)
                .Select(p =>
                {
                    try { return $"{p.Name}={TruncateToString(p.GetValue(value), 40)}"; }
                    catch { return $"{p.Name}=?"; }
                });
            var propStr = string.Join(", ", props);
            if (!string.IsNullOrEmpty(propStr))
                return $"{{ {propStr} }}";
        }

        return TruncateToString(value, 80);
    }

    private static string TruncateToString(object? value, int max)
    {
        if (value == null) return "null";
        if (value is string s) return s.Length > max ? $"\"{s[..max]}...[{s.Length - max} more chars]\"" : $"\"{s}\"";
        var str = value.ToString() ?? "?";
        return str.Length > max ? $"{str[..max]}...[{str.Length - max} more chars]" : str;
    }
}

public enum DebugEvent { OnCreate, OnChange, OnDelete }

public class DebugVariable
{
    public string Name { get; set; } = "";
    public DebugEvent? Event { get; set; }
}
