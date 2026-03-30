using System.Reflection;
using System.Text.Json;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace PLang.Runtime2.modules.builder.providers;

public class DefaultBuilderProvider : IBuilderProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private static Data? BuildingGuard(IContext action)
    {
        if (!action.Context.Engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));
        return null;
    }

    // --- Actions ---

    public Task<Data> GetActions(GetActions action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return Task.FromResult(guard);

        var engine = action.Context.Engine;
        var modules = engine.Modules;
        var result = new Actions();

        foreach (var ns in modules.Names)
        {
            foreach (var className in modules.GetActions(ns))
            {
                var parameterType = modules.GetActionType(ns, className);
                if (parameterType == null) continue;

                var parameters = new List<Data>();
                var nCtx = new NullabilityInfoContext();

                foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;

                    var typeName = TypeMapping.GetTypeName(prop.PropertyType);

                    bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null;
                    if (!isNullable && !prop.PropertyType.IsValueType)
                        isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;
                    if (isNullable && !typeName.EndsWith("?"))
                        typeName += "?";

                    var validValues = TypeMapping.GetValidValues(prop.PropertyType);
                    if (validValues != null)
                        typeName += $"({string.Join("|", validValues)})";

                    var hasVar = prop.GetCustomAttribute<VariableNameAttribute>() != null;
                    var defaultAttr = prop.GetCustomAttribute<DefaultAttribute>();

                    var desc = hasVar ? $"@var {typeName}" : typeName;
                    if (defaultAttr != null)
                        desc += $" = {FormatDefault(defaultAttr.Value)}";

                    parameters.Add(new Data(prop.Name, desc));
                }

                bool cacheable = true;
                var actionAttr = parameterType.GetCustomAttribute<ActionAttribute>();
                if (actionAttr != null)
                    cacheable = actionAttr.Cacheable;

                result.Add(new Action
                {
                    Module = ns,
                    ActionName = className,
                    ParameterSchema = parameterType,
                    Parameters = parameters,
                    Cacheable = cacheable
                });
            }
        }

        return Task.FromResult(Data.Ok(result));
    }

    // --- Types ---

    public Data GetTypes(types action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var names = TypeMapping.GetBuilderTypeNames();
        var schemas = TypeMapping.GetComplexTypeSchemas();
        var schemaLines = schemas.Select(kvp => $"  {kvp.Key}: {kvp.Value}");

        var result = new
        {
            TypeNames = string.Join(", ", names),
            TypeSchemas = string.Join("\n", schemaLines)
        };

        return Data.Ok(result);
    }

    // --- Goals ---

    public async Task<Data> GetGoals(goals action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var engine = action.Context.Engine;
        var context = action.Context;
        var searchPath = string.IsNullOrWhiteSpace(action.Path) ? "." : action.Path;

        var listAction = new file.List
        {
            Context = context,
            Path = new PLangPath(searchPath, context),
            Pattern = "*.goal",
            Recursive = true
        };
        var listResult = await engine.RunAction(listAction, context);
        if (!listResult.Success)
            return listResult;

        var files = listResult.Value as PLangPath[];
        if (files == null || files.Length == 0)
            return Data.Ok(new List<Goal>());

        var allGoals = new List<Goal>();

        foreach (var file in files)
        {
            var readAction = new file.Read { Context = context, Path = file };
            var readResult = await engine.RunAction(readAction, context);
            if (!readResult.Success) continue;

            var text = readResult.Value?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var relativePath = file.Relative ?? file.Raw;
            if (!relativePath.StartsWith('/') && !relativePath.StartsWith('\\'))
                relativePath = "/" + relativePath;

            var parsedGoals = Goal.Parse(text, relativePath);

            var normalizedPath = relativePath.Replace('\\', '/');
            if (normalizedPath.StartsWith("/system/", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var goal in parsedGoals)
                    goal.IsSystem = true;
            }

            foreach (var goal in parsedGoals)
                await MergePrData(goal, engine, context);

            allGoals.AddRange(parsedGoals);
        }

        return Data.Ok(allGoals);
    }

    public async Task<Data> SaveGoals(goalsSave action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var engine = action.Context.Engine;
        var context = action.Context;

        if (action.Goals.Count == 0)
            return Data.FromError(new Engine.Errors.ActionError("No goals to save", "NoGoals", 400));

        var prPath = action.Goals[0].PrPath;
        if (string.IsNullOrEmpty(prPath))
            return Data.FromError(new Engine.Errors.ActionError("Goals have no Path set, cannot derive PrPath", "NoPrPath", 400));

        var json = JsonSerializer.Serialize(action.Goals, Json.PrFileWrite);

        var saveAction = new file.Save
        {
            Context = context,
            Path = new PLangPath(prPath, context),
            Value = new Data("", json)
        };
        var saveResult = await engine.RunAction(saveAction, context);
        return saveResult.Success ? Data.Ok(true) : saveResult;
    }

    // --- Validate ---

    public async Task<Data> Validate(validate action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var engine = action.Context.Engine;
        var context = action.Context;
        var modules = engine.Modules;

        var notFound = new List<string>();
        foreach (var a in action.Actions)
        {
            if (!modules.Contains(a.Module, a.ActionName))
                notFound.Add($"{a.Module}.{a.ActionName}");
        }

        if (notFound.Count > 0)
            return Data.FromError(new Engine.Errors.ActionError(
                $"Actions not found: {string.Join(", ", notFound)}", "ActionNotFound", 400));

        await ResolveGoalCallPaths(action.Actions, engine, context);

        foreach (var a in action.Actions)
            FillDefaults(a, modules);

        return Data.Ok(true);
    }

    // --- Merge ---

    public Data Merge(merge action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        action.Step.Merge(action.StepFromLlm);
        return Data.Ok(action.Step);
    }

    // --- App ---

    public async Task<Data> GetApp(app action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var engine = action.Context.Engine;
        var context = action.Context;

        var basePath = string.IsNullOrWhiteSpace(action.Path) || action.Path == "." ? "" : action.Path;
        var appPrPath = string.IsNullOrEmpty(basePath)
            ? ".build/app.pr"
            : basePath.TrimEnd('/', '\\') + "/.build/app.pr";

        var readAction = new file.Read
        {
            Context = context,
            Path = new PLangPath(appPrPath, context)
        };
        var readResult = await engine.RunAction(readAction, context);

        if (readResult.Success && readResult.Value?.ToString() is string json && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<AppData>(json, Json.CaseInsensitiveRead);
                if (existing != null)
                    return Data.Ok(existing);
            }
            catch (JsonException) { }
        }

        var newApp = new AppData
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Version = "0.2"
        };

        var saveJson = JsonSerializer.Serialize(newApp, Json.CamelCaseIndented);
        var saveAction = new file.Save
        {
            Context = context,
            Path = new PLangPath(appPrPath, context),
            Value = new Data("", saveJson)
        };
        var saveResult = await engine.RunAction(saveAction, context);
        return saveResult.Success ? Data.Ok(newApp) : saveResult;
    }

    public async Task<Data> SaveApp(appSave action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var engine = action.Context.Engine;
        var context = action.Context;

        action.App.Updated = DateTime.UtcNow;

        var savePath = string.IsNullOrWhiteSpace(action.Path) ? ".build/app.pr" : action.Path;
        var json = JsonSerializer.Serialize(action.App, Json.CamelCaseIndented);

        var saveAction = new file.Save
        {
            Context = context,
            Path = new PLangPath(savePath, context),
            Value = new Data("", json)
        };
        var saveResult = await engine.RunAction(saveAction, context);
        return saveResult.Success ? Data.Ok(action.App) : saveResult;
    }

    // --- Private helpers ---

    private static async Task MergePrData(Goal goal, Engine.@this engine,
        Engine.Context.PLangContext context)
    {
        var prPath = goal.PrPath;
        if (string.IsNullOrEmpty(prPath)) return;

        var readAction = new file.Read
        {
            Context = context,
            Path = new PLangPath(prPath, context)
        };
        var readResult = await engine.RunAction(readAction, context);
        if (!readResult.Success) return;

        var prJson = readResult.Value?.ToString();
        if (string.IsNullOrWhiteSpace(prJson)) return;

        try
        {
            var prGoals = JsonSerializer.Deserialize<List<Goal>>(prJson, Json.CaseInsensitiveRead);
            if (prGoals != null)
            {
                var match = prGoals.FirstOrDefault(g =>
                    g.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    goal.MergeFrom(match);
                return;
            }
        }
        catch (JsonException ex)
        {
            goal.Errors.Add(new Engine.Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}: {ex.Message}"
            });
        }
    }

    private static async Task ResolveGoalCallPaths(Actions actions, Engine.@this engine,
        Engine.Context.PLangContext context)
    {
        foreach (var action in actions)
        {
            if (action.Parameters == null) continue;

            foreach (var param in action.Parameters)
            {
                if (!string.Equals(param.Type?.Value, "goal.call", StringComparison.OrdinalIgnoreCase))
                    continue;

                var goalCall = ToGoalCall(param.Value);
                if (goalCall == null || string.IsNullOrEmpty(goalCall.Name))
                    continue;

                if (goalCall.Name.Contains('%'))
                {
                    param.Value = goalCall;
                    continue;
                }

                var expectedPrPath = ComputeExpectedPrPath(goalCall.Name);
                if (expectedPrPath != null)
                {
                    var readAction = new file.Read
                    {
                        Context = context,
                        Path = new PLangPath(expectedPrPath, context)
                    };
                    var readResult = await engine.RunAction(readAction, context);
                    if (readResult.Success)
                    {
                        goalCall.PrPath = expectedPrPath;
                        param.Value = goalCall;
                        continue;
                    }
                }

                param.Value = goalCall;
            }
        }
    }

    private static string? ComputeExpectedPrPath(string goalName)
    {
        var name = goalName;
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];

        if (goalName.StartsWith('/') || goalName.StartsWith('\\'))
        {
            var lastSep = name.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSep >= 0)
            {
                var dir = name[..(lastSep + 1)];
                var baseName = name[(lastSep + 1)..].ToLowerInvariant();
                return dir + ".build/" + baseName + ".pr";
            }
            return "/.build/" + name.ToLowerInvariant() + ".pr";
        }

        return ".build/" + name.ToLowerInvariant() + ".pr";
    }

    private static GoalCall? ToGoalCall(object? value)
    {
        if (value is GoalCall gc) return gc;
        return TypeMapping.ConvertTo<GoalCall>(value);
    }

    private static void FillDefaults(Action action, Engine.Modules.@this modules)
    {
        var actionType = modules.GetActionType(action.Module, action.ActionName);
        if (actionType == null) return;

        var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (action.Parameters != null)
            foreach (var p in action.Parameters) paramNames.Add(p.Name);

        var configType = GetConfigureType(actionType);
        action.Defaults = configType != null
            ? FillFromConfigInstance(configType, paramNames)
            : FillFromAttributes(actionType, paramNames);
    }

    private static System.Type? GetConfigureType(System.Type actionType)
    {
        foreach (var iface in actionType.GetInterfaces())
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IConfigure<>))
                return iface.GetGenericArguments()[0];
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
            var attrs = prop.GetCustomAttributes(typeof(DefaultAttribute), false);
            if (attrs.Length == 0) continue;
            defaults.Add(new Data(prop.Name.ToLowerInvariant(), ((DefaultAttribute)attrs[0]).Value));
        }
        return defaults;
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "null"
    };
}
