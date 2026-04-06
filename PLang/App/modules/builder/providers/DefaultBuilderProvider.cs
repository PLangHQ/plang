using App.Utils;
using System.Text.Json;
using App.Goals.Goal;
using App.Variables;
using Goal = App.Goals.Goal.@this;
using Actions = App.Goals.Goal.Steps.Step.Actions.@this;

namespace App.modules.builder.providers;

public class DefaultBuilderProvider : IBuilderProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private static Data? BuildingGuard(IContext action)
    {
        if (!action.Context.App.Building.IsEnabled)
            return Data.FromError(new Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));
        return null;
    }

    // --- Actions ---

    public Task<Data> Actions(GetActions action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return Task.FromResult(guard);

        return Task.FromResult(Data.Ok(action.Context.App.Modules.Describe()));
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

        var engine = action.Context.App;
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

        // Filter by engine.Building.Files if set (--build={"files":"test.goal"})
        var buildFiles = engine.Building.Files;
        if (buildFiles.Count > 0)
        {
            files = files.Where(f =>
                buildFiles.Any(bf => f.FileName.Equals(bf.FileName, StringComparison.OrdinalIgnoreCase)
                    || f.Relative.EndsWith(bf.Relative, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (files.Length == 0)
                return Data.Ok(new List<Goal>());
        }

        var allGoals = new List<Goal>();
        var allErrors = new List<Info>();

        foreach (var file in files)
        {
            var readAction = new file.Read { Context = context, Path = file };
            var readResult = await engine.RunAction(readAction, context);
            if (!readResult.Success)
            {
                allErrors.Add(new Info
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

        var engine = action.Context.App;
        var context = action.Context;

        if (action.Goals.Count == 0)
            return Data.FromError(new Errors.ActionError("No goals to save", "NoGoals", 400));

        // Apply LLM-generated description if available in Variables
        var stepResults = context.Variables.Get("stepResults");
        if (stepResults?.Value is IDictionary<string, object?> resultsDict
            && resultsDict.TryGetValue("description", out var desc)
            && desc is string description
            && !string.IsNullOrEmpty(description))
        {
            action.Goals[0].Description = description;
        }

        var prPath = action.Goals[0].PrPath;
        if (string.IsNullOrEmpty(prPath))
            return Data.FromError(new Errors.ActionError("Goals have no Path set, cannot derive PrPath", "NoPrPath", 400));

        // Load existing goals from .pr file — merge by name (replace or append)
        var existingGoals = new List<Goal>();
        var readAction = new file.Read { Context = context, Path = new PLangPath(prPath, context) };
        var readResult = await engine.RunAction(readAction, context);
        if (readResult.Success && readResult.Value?.ToString() is string existingJson && !string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<Goal>>(existingJson, Json.CaseInsensitiveRead);
                if (parsed != null) existingGoals = parsed;
            }
            catch (JsonException) { /* corrupt file — start fresh */ }
        }

        // Replace existing goals by name, append new ones
        foreach (var goal in action.Goals)
        {
            var idx = existingGoals.FindIndex(g =>
                string.Equals(g.Name, goal.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                existingGoals[idx] = goal;
            else
                existingGoals.Add(goal);
        }

        var json = JsonSerializer.Serialize(existingGoals, Json.PrWrite);

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

        var engine = action.Context.App;
        var context = action.Context;
        var modules = engine.Modules;

        if (action.Actions == null || action.Actions.Count == 0)
            return Data.Ok(true);

        var notFound = new List<string>();
        foreach (var a in action.Actions)
        {
            if (!modules.Contains(a.Module, a.ActionName))
                notFound.Add($"{a.Module}.{a.ActionName}");
        }

        if (notFound.Count > 0)
            return Data.FromError(new Errors.ActionError(
                $"Actions not found: {string.Join(", ", notFound)}", "ActionNotFound", 400));

        await ResolveGoalCallPaths(action.Actions, engine, context);
        NormalizeParameterTypes(action.Actions);

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

        var engine = action.Context.App;
        // App loads its identity from app.pr at Start() — just return it
        return Data.Ok(engine);
    }

    public async Task<Data> AppSave(appSave action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        return await action.Context.App.Save();
    }

    /// <summary>
    /// Normalizes parameter values to match their declared type.
    /// LLMs are non-deterministic — they may produce "false" (string) instead of false (bool).
    /// This runs at build time so the .pr file has correct types.
    /// </summary>
    private static void NormalizeParameterTypes(Actions actions)
    {
        foreach (var a in actions)
        {
            if (a.Parameters == null) continue;
            foreach (var p in a.Parameters)
            {
                if (p.Value is not string strValue) continue;
                if (strValue.StartsWith('%') && strValue.EndsWith('%')) continue; // variable reference
                if (p.Type == null) continue;

                var targetType = TypeMapping.GetType(p.Type.Value);
                if (targetType == null || targetType == typeof(string)) continue;

                var (converted, _) = TypeMapping.TryConvertTo(strValue, targetType);
                if (converted != null)
                    p.Value = converted;
            }
        }
    }

    // --- Private helpers ---

    /// <summary>
    /// Merges existing .pr data into a goal. Returns any errors encountered (corrupt .pr files).
    /// </summary>
    private static async Task<List<Info>> MergePrData(Goal goal, App.@this engine,
        Context.@this context)
    {
        var errors = new List<Info>();
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
            errors.Add(new Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}: {ex.Message}"
            });
        }

        return errors;
    }

    private static async Task ResolveGoalCallPaths(Actions actions, App.@this engine,
        Context.@this context)
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
