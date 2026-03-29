using System.Reflection;
using System.Text.Json;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Validates LLM-returned actions exist, resolves GoalCall paths, fills defaults.
/// </summary>
[Action("validateActions")]
public partial class validateActions : IContext
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

        // Resolve GoalCall paths
        ResolveGoalCallPaths(Actions, engine);

        // Fill defaults
        foreach (var action in Actions)
            FillDefaults(action, modules);

        return Data.Ok(true);
    }

    private static void ResolveGoalCallPaths(Actions actions, Engine.@this engine)
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

                // Try to find .pr file in .build directories
                var fs = engine.FileSystem;
                var resolved = TryResolvePrPath(goalCall.Name, fs);
                if (resolved != null)
                {
                    goalCall.PrPath = resolved;
                    param.Value = goalCall;
                    continue;
                }

                // Not found — leave PrPath null (runtime falls back to name lookup)
                param.Value = goalCall;
            }
        }
    }

    private static string? TryResolvePrPath(string goalName, Interfaces.IPLangFileSystem fs)
    {
        // Scan .build directories for matching .pr files
        try
        {
            var buildDirs = fs.Directory.GetDirectories(fs.RootDirectory, ".build", SearchOption.AllDirectories);
            foreach (var buildDir in buildDirs)
            {
                var prFiles = fs.Directory.GetFiles(buildDir, "*.pr");
                foreach (var prFile in prFiles)
                {
                    try
                    {
                        var json = fs.File.ReadAllText(prFile);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                        // Try List<Goal> format
                        var goals = JsonSerializer.Deserialize<List<Engine.Goals.Goal.@this>>(json, options);
                        if (goals != null)
                        {
                            var match = goals.FirstOrDefault(g =>
                                g.Name.Equals(goalName, StringComparison.OrdinalIgnoreCase));
                            if (match?.PrPath != null)
                                return match.PrPath;
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip corrupt files
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Can't scan — return null
        }

        return null;
    }

    private static GoalCall? DeserializeGoalCall(object? value)
    {
        if (value is GoalCall gc) return gc;

        if (value is JsonElement je)
        {
            try
            {
                return JsonSerializer.Deserialize<GoalCall>(je.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

        // Check for IConfigure<TConfig>
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
