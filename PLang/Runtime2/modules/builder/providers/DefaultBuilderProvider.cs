using System.Text.Json;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
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

    public Task<Data> Actions(GetActions action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return Task.FromResult(guard);

        return Task.FromResult(Data.Ok(action.Context.Engine.Modules.Describe()));
    }

    // --- Types ---

    public Data Types(types action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var names = TypeMapping.GetBuilderTypeNames();
        var schemas = TypeMapping.GetComplexTypeSchemas();
        var schemaLines = schemas.Select(kvp => $"  {kvp.Key}: {kvp.Value}");

        return Data.Ok(new BuilderTypeInfo(
            string.Join(", ", names),
            string.Join("\n", schemaLines)));
    }

    // --- Goals ---

    public async Task<Data> Goals(goals action)
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
        var allErrors = new List<Engine.Info>();

        foreach (var file in files)
        {
            var readAction = new file.Read { Context = context, Path = file };
            var readResult = await engine.RunAction(readAction, context);
            if (!readResult.Success)
            {
                allErrors.Add(new Engine.Info
                {
                    Key = "FileReadError",
                    Message = $"Failed to read {file.Raw}: {readResult.Error?.Message}"
                });
                continue;
            }

            var text = readResult.Value?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var relativePath = file.Relative ?? file.Raw;
            if (!relativePath.StartsWith('/') && !relativePath.StartsWith('\\'))
                relativePath = "/" + relativePath;

            var parsedGoals = Goal.Parse(text, relativePath);

            // Merge existing .pr data, collect errors
            foreach (var goal in parsedGoals)
            {
                var mergeErrors = await MergePrData(goal, engine, context);
                allErrors.AddRange(mergeErrors);
            }

            allGoals.AddRange(parsedGoals);
        }

        var result = Data.Ok(allGoals);
        if (allErrors.Count > 0)
            result.Warnings = allErrors;
        return result;
    }

    public async Task<Data> GoalsSave(goalsSave action)
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

        var json = JsonSerializer.Serialize(action.Goals, Json.CamelCaseIndented);

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
        {
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (a.Parameters != null)
                foreach (var p in a.Parameters) paramNames.Add(p.Name);
            a.Defaults = modules.GetDefaults(a.Module, a.ActionName, paramNames);
        }

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

    public async Task<Data> App(app action)
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
            catch (JsonException ex)
            {
                return Data.FromError(new Engine.Errors.ActionError(
                    $"Failed to parse app.pr: {ex.Message}", "CorruptAppFile", 400));
            }
        }

        return Data.Ok((AppData?)null);
    }

    public async Task<Data> AppSave(appSave action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var engine = action.Context.Engine;
        var context = action.Context;

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

    /// <summary>
    /// Merges existing .pr data into a goal. Returns any errors encountered (corrupt .pr files).
    /// </summary>
    private static async Task<List<Engine.Info>> MergePrData(Goal goal, Engine.@this engine,
        Engine.Context.PLangContext context)
    {
        var errors = new List<Engine.Info>();
        var prPath = goal.PrPath;
        if (string.IsNullOrEmpty(prPath)) return errors;

        var readAction = new file.Read
        {
            Context = context,
            Path = new PLangPath(prPath, context)
        };
        var readResult = await engine.RunAction(readAction, context);
        if (!readResult.Success) return errors;

        var prJson = readResult.Value?.ToString();
        if (string.IsNullOrWhiteSpace(prJson)) return errors;

        try
        {
            var prGoals = JsonSerializer.Deserialize<List<Goal>>(prJson, Json.CaseInsensitiveRead);
            if (prGoals != null)
            {
                var match = prGoals.FirstOrDefault(g =>
                    g.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    goal.MergeFrom(match);
            }
        }
        catch (JsonException ex)
        {
            errors.Add(new Engine.Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}: {ex.Message}"
            });
        }

        return errors;
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

                // Derive PrPath using Goal's own convention
                var goalPath = goalCall.Name;
                if (!goalPath.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
                    goalPath += ".goal";
                if (!goalPath.StartsWith('/') && !goalPath.StartsWith('\\'))
                    goalPath = "/" + goalPath;

                var tempGoal = new Goal { Path = goalPath };
                var expectedPrPath = tempGoal.PrPath;
                if (expectedPrPath != null)
                {
                    // Check existence via file.exists — don't read the whole file
                    var existsAction = new file.Exists
                    {
                        Context = context,
                        Path = new PLangPath(expectedPrPath, context)
                    };
                    var existsResult = await engine.RunAction(existsAction, context);
                    if (existsResult.Success && existsResult is PLangPath pathData && pathData.Exists)
                    {
                        goalCall.PrPath = expectedPrPath;
                    }
                }

                param.Value = goalCall;
            }
        }
    }

    private static GoalCall? ToGoalCall(object? value)
    {
        if (value is GoalCall gc) return gc;
        return TypeMapping.ConvertTo<GoalCall>(value);
    }
}
