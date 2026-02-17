using System.Text;
using System.Text.RegularExpressions;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine;

/// <summary>
/// Provides debug output for PLang execution when !debug is passed on the command line.
/// Registers events to dump step info, call stack, and memory stack to stderr.
/// </summary>
public static class EngineDebug
{
    public static void Apply(Engine engine, object debugValue)
    {
        engine.IsDebugMode = true;

        string? goalFilter = null;
        int? stepFilter = null;

        if (debugValue is string spec && !string.IsNullOrEmpty(spec))
        {
            ParseFilter(spec, out goalFilter, out stepFilter);
        }

        var events = engine.Context.User.Events;

        events.Register(
            EventType.BeforeStep,
            context => BeforeStepHandler(context, stepFilter),
            goalNamePattern: goalFilter ?? "*",
            priority: int.MaxValue,
            stopOnError: false
        );

        events.Register(
            EventType.AfterStep,
            context => AfterStepHandler(context, stepFilter),
            goalNamePattern: goalFilter ?? "*",
            priority: int.MaxValue,
            stopOnError: false
        );

        events.Register(
            EventType.AfterGoal,
            AfterGoalHandler,
            goalNamePattern: goalFilter ?? "*",
            priority: int.MaxValue,
            stopOnError: false
        );
    }

    private static void ParseFilter(string spec, out string? goalFilter, out int? stepFilter)
    {
        goalFilter = null;
        stepFilter = null;

        var colonIndex = spec.IndexOf(':');
        if (colonIndex >= 0)
        {
            var goalPart = spec[..colonIndex];
            var stepPart = spec[(colonIndex + 1)..];

            goalFilter = goalPart == "*" ? null : goalPart;
            if (int.TryParse(stepPart, out var idx))
                stepFilter = idx;
        }
        else
        {
            goalFilter = spec;
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
            var paramStr = string.Join(", ", action.Parameters.Select(p => $"{p.Name}={p.Value}"));
            sb.AppendLine($"  Action: {action.Module}.{action.ActionName}({paramStr})");

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
                sb.AppendLine($"    %{name}% = (undefined)");
            else
                sb.AppendLine($"    %{name}% = {FormatValue(data.Value)} ({data.Type?.Value ?? "?"})");
        }
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "(null)";
    }
}
