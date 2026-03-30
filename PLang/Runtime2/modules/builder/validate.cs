using System.Reflection;
using System.Text.Json;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Validates LLM-returned actions exist, resolves GoalCall paths relative to the
/// goal being built, fills defaults.
/// </summary>
[Action("actions.validate")]
public partial class validate : IContext
{
    [IsNotNull]
    public partial Actions Actions { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        var modules = engine.Modules;

        // Check all actions exist
        var notFound = new List<string>();
        foreach (var action in Actions)
        {
            if (!modules.Contains(action.Module, action.ActionName))
                notFound.Add($"{action.Module}.{action.ActionName}");
        }

        if (notFound.Count > 0)
            return Data.FromError(new Engine.Errors.ActionError(
                $"Actions not found: {string.Join(", ", notFound)}", "ActionNotFound", 400));

        // Resolve GoalCall paths relative to current goal
        await ResolveGoalCallPaths(Actions, engine);

        // Fill defaults
        foreach (var action in Actions)
            FillDefaults(action, modules);

        return Data.Ok(true);
    }

    private async Task ResolveGoalCallPaths(Actions actions, Engine.@this engine)
    {
        foreach (var action in actions)
        {
            if (action.Parameters == null) continue;

            foreach (var param in action.Parameters)
            {
                if (!string.Equals(param.Type?.Value, "goal.call", StringComparison.OrdinalIgnoreCase))
                    continue;

                var goalCall = DeserializeGoalCall(param.Value);
                if (goalCall == null || string.IsNullOrEmpty(goalCall.Name))
                    continue;

                // Dynamic name — can't resolve at build time
                if (goalCall.Name.Contains('%'))
                {
                    param.Value = goalCall;
                    continue;
                }

                // Resolve path relative to current goal:
                // /SomeGoal = from engine root, SomeGoal = from goal's directory
                var goalName = goalCall.Name;
                var expectedPrPath = ComputeExpectedPrPath(goalName, engine);

                if (expectedPrPath != null)
                {
                    // Verify the .pr file exists via file.read
                    var readAction = new file.Read
                    {
                        Context = Context,
                        Path = new PLangPath(expectedPrPath, Context)
                    };
                    var readResult = await engine.RunAction(readAction, Context);
                    if (readResult.Success)
                    {
                        goalCall.PrPath = expectedPrPath;
                        param.Value = goalCall;
                        continue;
                    }
                }

                // Not found — leave PrPath null (runtime falls back to name lookup)
                param.Value = goalCall;
            }
        }
    }

    /// <summary>
    /// Computes the expected PrPath for a goal name.
    /// /GoalName = from root, GoalName = from current goal's directory.
    /// </summary>
    private static string? ComputeExpectedPrPath(string goalName, Engine.@this engine)
    {
        // Normalize: strip .goal extension if present
        var name = goalName;
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];

        // The PrPath convention: /folder/.build/name.pr
        // For a goal name "DoSomething", the .goal file is DoSomething.goal
        // and the .pr file is .build/dosomething.pr (relative to the .goal file's directory)
        var lowerName = name.ToLowerInvariant();

        if (goalName.StartsWith('/') || goalName.StartsWith('\\'))
        {
            // Absolute from root: /folder/GoalName → /folder/.build/goalname.pr
            var lastSep = name.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSep >= 0)
            {
                var dir = name[..(lastSep + 1)];
                var baseName = name[(lastSep + 1)..].ToLowerInvariant();
                return dir + ".build/" + baseName + ".pr";
            }
            return "/.build/" + lowerName + ".pr";
        }

        // Relative — from the current goal's directory
        return ".build/" + lowerName + ".pr";
    }

    private static GoalCall? DeserializeGoalCall(object? value)
    {
        if (value is GoalCall gc) return gc;

        if (value is JsonElement je)
        {
            try
            {
                return JsonSerializer.Deserialize<GoalCall>(je.GetRawText(), JsonOptions.CaseInsensitive);
            }
            catch { return null; }
        }

        if (value is string s)
            return new GoalCall { Name = s };

        return null;
    }

    private static void FillDefaults(Action action, Engine.Modules.@this modules)
    {
        var actionType = modules.GetActionType(action.Module, action.ActionName);
        if (actionType == null) return;

        var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (action.Parameters != null)
        {
            foreach (var p in action.Parameters)
                paramNames.Add(p.Name);
        }

        var configType = GetConfigureType(actionType);
        action.Defaults = configType != null
            ? FillFromConfigInstance(configType, paramNames)
            : FillFromAttributes(actionType, paramNames);
    }

    private static System.Type? GetConfigureType(System.Type actionType)
    {
        foreach (var iface in actionType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IConfigure<>))
                return iface.GetGenericArguments()[0];
        }
        return null;
    }

    private static List<Data> FillFromConfigInstance(System.Type configType, HashSet<string> paramNames)
    {
        var defaults = new List<Data>();
        var instance = Activator.CreateInstance(configType);
        if (instance == null) return defaults;

        foreach (var prop in configType.GetProperties())
        {
            if (paramNames.Contains(prop.Name)) continue;
            var value = prop.GetValue(instance);
            if (value == null) continue;
            defaults.Add(new Data(prop.Name.ToLowerInvariant(), value));
        }
        return defaults;
    }

    private static List<Data> FillFromAttributes(System.Type actionType, HashSet<string> paramNames)
    {
        var defaults = new List<Data>();
        foreach (var prop in actionType.GetProperties())
        {
            if (paramNames.Contains(prop.Name)) continue;
            var defaultAttr = prop.GetCustomAttributes(typeof(DefaultAttribute), false);
            if (defaultAttr.Length == 0) continue;
            var attr = (DefaultAttribute)defaultAttr[0];
            defaults.Add(new Data(prop.Name.ToLowerInvariant(), attr.Value));
        }
        return defaults;
    }
}
