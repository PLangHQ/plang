using System.Text;
using System.Text.RegularExpressions;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;
using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;

namespace PLang.Runtime2.Engine.Debug;

/// <summary>
/// Provides debug output for PLang execution when !debug is passed on the command line.
/// Registers events to dump step info, call stack, and memory stack to stderr.
/// </summary>
public sealed class @this
{
    private readonly Engine.@this _engine;

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    public @this(Engine.@this engine)
    {
        _engine = engine;
    }

    public void Apply(object debugValue)
    {
        IsEnabled = true;

        string? goalFilter = null;
        int? stepFilter = null;

        if (debugValue is IDictionary<string, object?> dict)
        {
            ParseJsonFilter(dict, out goalFilter, out stepFilter);
        }
        else if (debugValue is not true and not false)
        {
            // Newtonsoft JObject or other dictionary-like types — convert via generic dictionary
            var converted = ToDictionary(debugValue);
            if (converted != null)
                ParseJsonFilter(converted, out goalFilter, out stepFilter);
        }

        var events = _engine.Context.User.Events;

        events.Register(new EventBinding(
            EventType.BeforeStep,
            context => BeforeStepHandler(context, stepFilter),
            goalNamePattern: goalFilter ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        events.Register(new EventBinding(
            EventType.AfterStep,
            context => AfterStepHandler(context, stepFilter),
            goalNamePattern: goalFilter ?? "*",
            priority: int.MaxValue,
            stopOnError: false));

        events.Register(new EventBinding(
            EventType.AfterGoal,
            AfterGoalHandler,
            goalNamePattern: goalFilter ?? "*",
            priority: int.MaxValue,
            stopOnError: false));
    }

    private static IDictionary<string, object?>? ToDictionary(object value)
    {
        // Handle Newtonsoft JObject and similar dictionary-like types
        if (value is System.Collections.IDictionary rawDict)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in rawDict)
                result[entry.Key.ToString()!] = entry.Value;
            return result;
        }

        // Try indexer pattern (JObject implements this)
        var type = value.GetType();
        var indexer = type.GetProperty("Item", new[] { typeof(string) });
        if (indexer == null) return null;

        var result2 = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        // Try known property names
        foreach (var name in new[] { "goal", "step", "goals", "verbose" })
        {
            try
            {
                var val = indexer.GetValue(value, new object[] { name });
                if (val != null) result2[name] = val.ToString();
            }
            catch { }
        }
        return result2.Count > 0 ? result2 : null;
    }

    private static void ParseJsonFilter(IDictionary<string, object?> dict, out string? goalFilter, out int? stepFilter)
    {
        goalFilter = null;
        stepFilter = null;

        if (dict.TryGetValue("goal", out var goalVal) && goalVal is string g && !string.IsNullOrEmpty(g))
            goalFilter = g;

        if (dict.TryGetValue("step", out var stepVal))
        {
            if (stepVal is int i) stepFilter = i;
            else if (stepVal is long l) stepFilter = (int)l;
            else if (stepVal is string s && int.TryParse(s, out var parsed)) stepFilter = parsed;
        }
    }

    private static Task<Data> BeforeStepHandler(PLangContext context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(Data.Ok());
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return Task.FromResult(Data.Ok());

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [BEFORE]: Step [{step.Index}] of {goalName} ===");
        sb.AppendLine($"  Text: {step.Text}");

        foreach (var action in step.Actions)
        {
            sb.AppendLine($"  Action: {action.Module}.{action.ActionName}");
            foreach (var p in action.Parameters)
            {
                sb.AppendLine($"    {p.Name} = {FormatValue(p.Value)}");
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

        AppendStepVariables(sb, step, context.MemoryStack);
        sb.AppendLine("========================================");

        Console.Error.Write(sb);
        return Task.FromResult(Data.Ok());
    }

    private static Task<Data> AfterStepHandler(PLangContext context, int? stepFilter)
    {
        var step = context.Step;
        if (step == null) return Task.FromResult(Data.Ok());
        if (stepFilter.HasValue && step.Index != stepFilter.Value) return Task.FromResult(Data.Ok());

        var goalName = context.Goal?.Name ?? "?";
        var sb = new StringBuilder();

        sb.AppendLine($"=== DEBUG [AFTER]: Step [{step.Index}] of {goalName} ===");

        AppendStepVariables(sb, step, context.MemoryStack);
        sb.AppendLine("========================================");

        Console.Error.Write(sb);
        return Task.FromResult(Data.Ok());
    }

    private static Task<Data> AfterGoalHandler(PLangContext context)
    {
        var goalName = context.Goal?.Name ?? "?";
        Console.Error.WriteLine($"--- DEBUG: Goal '{goalName}' completed ---");
        return Task.FromResult(Data.Ok());
    }

    private static readonly Regex VarRefPattern = new(@"%([^%]+)%", RegexOptions.Compiled);

    private static void AppendStepVariables(StringBuilder sb, Step step, MemoryStack memoryStack)
    {
        var varNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in step.Actions)
        {
            // Collect from Return
            if (action.Return != null)
            {
                foreach (var r in action.Return)
                {
                    if (!string.IsNullOrEmpty(r.Name))
                        varNames.Add(r.Name);
                }
            }

            // Collect %var% references from parameter values
            foreach (var p in action.Parameters)
            {
                if (p.Value is string s)
                {
                    foreach (Match m in VarRefPattern.Matches(s))
                        varNames.Add(m.Groups[1].Value);
                }
            }
        }

        if (varNames.Count == 0) return;

        sb.AppendLine($"  Variables ({varNames.Count}):");
        foreach (var name in varNames)
        {
            var data = memoryStack.Get(name);
            if (data == null || !data.IsInitialized)
            {
                sb.AppendLine($"    %{name}% = (undefined)");
                continue;
            }

            sb.AppendLine($"    %{name}% = {FormatValue(data.Value)} ({data.Type?.Value ?? "?"})");

            if (data.Properties.Count > 0)
            {
                sb.AppendLine($"      Properties ({data.Properties.Count}):");
                foreach (var prop in data.Properties)
                {
                    sb.AppendLine($"        {prop.Name} = {FormatValue(prop.Value)}");
                }
            }
        }
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return s.Length > 500 ? $"\"{s[..500]}...\" ({s.Length} chars)" : $"\"{s}\"";
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
        return str.Length > 200 ? $"{str[..200]}... ({str.Length} chars)" : str;
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
        if (value is string s) return s.Length > max ? $"\"{s[..max]}...\"" : $"\"{s}\"";
        var str = value.ToString() ?? "?";
        return str.Length > max ? $"{str[..max]}..." : str;
    }
}
