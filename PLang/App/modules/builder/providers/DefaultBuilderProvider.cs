using System.Diagnostics;
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

    private static readonly Stopwatch _buildTimer = new();

    private static Data.@this? BuildingGuard(IContext action)
    {
        if (!action.Context.App.Building.IsEnabled)
            return Data.@this.FromError(new Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));
        return null;
    }

    // --- Actions ---

    public Task<Data.@this> Actions(GetActions action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return Task.FromResult(guard);

        return Task.FromResult(Data.@this.Ok(action.Context.App.Modules.Describe()));
    }

    // --- Types ---

    public Data.@this Types(types action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var names = TypeMapping.GetBuilderTypeNames();
        var schemas = TypeMapping.GetComplexTypeSchemas(action.Context.App.Modules);
        var schemaLines = schemas.Select(kvp => $"  {kvp.Key}: {kvp.Value}");

        return Data.@this.Ok(new BuilderTypeInfo(
            string.Join(", ", names),
            string.Join("\n", schemaLines)));
    }

    // --- Goals ---

    public async Task<Data.@this> Goals(goals action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var app = action.Context.App;
        var context = action.Context;
        var searchPath = string.IsNullOrWhiteSpace(action.Path) ? "." : action.Path;

        var listAction = new file.List
        {
            Context = context,
            Path = FileSystem.Path.Resolve(searchPath, context),
            Pattern = "*.goal",
            Recursive = true
        };
        var listResult = await app.RunAction(listAction, context);
        if (!listResult.Success)
            return listResult;

        var files = listResult.Value as FileSystem.Path[];
        if (files == null || files.Length == 0)
            return Data.@this.Ok(new List<Goal>());

        // Filter by app.Building.Files if set (--build={"files":"test.goal"})
        var buildFiles = app.Building.Files;
        if (buildFiles.Count > 0)
        {
            // Ensure filter paths have Context so FileName/Relative work
            foreach (var bf in buildFiles)
                bf.Context ??= context;

            files = files.Where(f =>
                buildFiles.Any(bf => f.FileName.Equals(bf.FileName, StringComparison.OrdinalIgnoreCase)
                    || f.Relative.EndsWith(bf.Relative, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (files.Length == 0)
                return Data.@this.Ok(new List<Goal>());
        }

        var allGoals = new List<Goal>();
        var allErrors = new List<Info>();

        foreach (var file in files)
        {
            var readAction = new file.Read { Context = context, Path = file };
            var readResult = await app.RunAction(readAction, context);
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

            var goal = Goal.Parse(text, relativePath);
            if (goal == null) continue;

            var mergeErrors = await MergePrData(goal, app, context);
            allErrors.AddRange(mergeErrors);

            allGoals.Add(goal);
        }

        _buildTimer.Restart();

        var result = Data.@this.Ok(allGoals);
        if (allErrors.Count > 0)
            result.Warnings = allErrors;
        return result;
    }

    public async Task<Data.@this> GoalsSave(goalsSave action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var app = action.Context.App;
        var context = action.Context;
        var goal = action.Goal;

        // Apply LLM-generated description if available in Variables
        var stepResults = context.Variables.Get("stepResults");
        if (stepResults.Value is IDictionary<string, object?> resultsDict
            && resultsDict.TryGetValue("description", out var desc)
            && desc is string description
            && !string.IsNullOrEmpty(description))
        {
            goal.Description = description;
        }

        var prPath = goal.PrPath;
        if (string.IsNullOrEmpty(prPath))
            return Data.@this.FromError(new Errors.ActionError("Goal has no Path set, cannot derive PrPath", "NoPrPath", 400));

        var json = JsonSerializer.Serialize(goal, Json.PrWrite);

        var saveAction = new file.Save
        {
            Context = context,
            Path = FileSystem.Path.Resolve(prPath, context),
            Value = new Data.@this("", json)
        };
        var saveResult = await app.RunAction(saveAction, context);

        var elapsed = _buildTimer.Elapsed;
        Console.WriteLine($"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s)");
        _buildTimer.Restart();

        return saveResult.Success ? Data.@this.Ok(true) : saveResult;
    }

    // --- Validate ---

    public async Task<Data.@this> Validate(validate action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var app = action.Context.App;
        var context = action.Context;
        var modules = app.Modules;

        if (action.Actions == null || action.Actions.Count == 0)
            return Data.@this.Ok(true);

        var notFound = new List<string>();
        foreach (var a in action.Actions)
        {
            if (!modules.Contains(a.Module, a.ActionName))
            {
                var available = modules.GetActions(a.Module);
                string suggestion;
                if (available.Any())
                {
                    var sorted = Utils.StringDistance.OrderBySimilarity(a.ActionName, available);
                    suggestion = $"Module '{a.Module}' exists but action '{a.ActionName}' not found. Did you mean: {string.Join(", ", sorted.Take(5))}?";
                }
                else
                {
                    var sorted = Utils.StringDistance.OrderBySimilarity(a.Module, modules.Names);
                    suggestion = $"Module '{a.Module}' not found. Did you mean: {string.Join(", ", sorted.Take(5))}?";
                }
                notFound.Add($"{a.Module}.{a.ActionName}: {suggestion}");
            }
        }

        if (notFound.Count > 0)
        {
            return Data.@this.FromError(new Errors.ActionError(
                $"Actions not found: {string.Join("; ", notFound)}",
                "ActionNotFound", 400));
        }

        await ResolveGoalCallPaths(action.Actions, app, context);
        NormalizeParameterTypes(action.Actions);

        var validationErrors = new List<string>();
        foreach (var a in action.Actions)
        {
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (a.Parameters != null)
                foreach (var p in a.Parameters) paramNames.Add(p.Name);
            a.Defaults = modules.GetDefaults(a.Module, a.ActionName, paramNames);

            // Action-level build validation
            var actionType = modules.GetActionType(a.Module, a.ActionName);
            if (actionType != null && typeof(IBuildValidatable).IsAssignableFrom(actionType))
            {
                var method = actionType.GetMethod("ValidateBuild",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    var error = (string?)method.Invoke(null, [a.Parameters]);
                    if (error != null)
                        validationErrors.Add($"{a.Module}.{a.ActionName}: {error}");
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            return Data.@this.FromError(new Errors.ActionError(
                string.Join("; ", validationErrors),
                "BuildValidation", 400));
        }

        return Data.@this.Ok(true);
    }

    // --- Merge ---

    public Data.@this Merge(merge action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        action.Step.Merge(action.StepFromLlm);
        return Data.@this.Ok(action.Step);
    }

    // --- App ---

    public async Task<Data.@this> App(app action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var app = action.Context.App;
        // App loads its identity from app.pr at Start() — just return it
        return Data.@this.Ok(app);
    }

    public async Task<Data.@this> AppSave(appSave action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        return await action.Context.App.Save();
    }

    // --- Promote Groups ---

    public Data.@this PromoteGroups(promoteGroups action)
    {
        var guard = BuildingGuard(action);
        if (guard != null) return guard;

        var steps = ToStepList(action.Steps);
        if (steps == null || steps.Count == 0)
            return Data.@this.Ok(action.Steps);

        // Collect groups and find the lowest level in each
        var groupLevels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps)
        {
            var group = GetString(step, "group");
            if (string.IsNullOrEmpty(group)) continue;

            var level = GetString(step, "level") ?? "high";
            if (!groupLevels.TryGetValue(group, out var current))
            {
                groupLevels[group] = level;
            }
            else
            {
                groupLevels[group] = LowestLevel(current, level);
            }
        }

        // Promote: if any group has a non-high level, set all members to that level
        int promoted = 0;
        foreach (var step in steps)
        {
            var group = GetString(step, "group");
            if (string.IsNullOrEmpty(group)) continue;

            if (!groupLevels.TryGetValue(group, out var groupLevel)) continue;
            if (string.Equals(groupLevel, "high", StringComparison.OrdinalIgnoreCase)) continue;

            var currentLevel = GetString(step, "level") ?? "high";
            if (string.Equals(currentLevel, "high", StringComparison.OrdinalIgnoreCase))
            {
                SetValue(step, "level", groupLevel);
                promoted++;
            }
        }

        if (promoted > 0)
            Console.WriteLine($"  Group promotion: {promoted} step(s) promoted to detail pass");

        return Data.@this.Ok(action.Steps);
    }

    private static string LowestLevel(string a, string b)
    {
        int Rank(string l) => l.ToLowerInvariant() switch
        {
            "low" => 0,
            "medium" => 1,
            _ => 2
        };
        return Rank(a) <= Rank(b) ? a : b;
    }

    private static List<object>? ToStepList(object? steps)
    {
        if (steps is List<object> list) return list;
        if (steps is List<object?> nullableList) return nullableList.Where(s => s != null).Select(s => s!).ToList();
        if (steps is System.Collections.IList rawList)
        {
            var result = new List<object>();
            foreach (var item in rawList)
                if (item != null) result.Add(item);
            return result;
        }
        return null;
    }

    private static string? GetString(object step, string key)
    {
        if (step is IDictionary<string, object?> dict && dict.TryGetValue(key, out var val))
            return val?.ToString();
        if (step is JsonElement je && je.TryGetProperty(key, out var prop))
            return prop.GetString();
        return null;
    }

    private static void SetValue(object step, string key, string value)
    {
        if (step is IDictionary<string, object?> dict)
            dict[key] = value;
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
    private static async Task<List<Info>> MergePrData(Goal goal, App.@this app,
        Actor.Context.@this context)
    {
        var errors = new List<Info>();
        var prPath = goal.PrPath;
        if (string.IsNullOrEmpty(prPath)) return errors;

        var readAction = new file.Read
        {
            Context = context,
            Path = FileSystem.Path.Resolve(prPath, context)
        };
        var readResult = await app.RunAction(readAction, context);
        if (!readResult.Success) return errors;

        // File provider auto-deserializes .pr files into a single Goal
        if (readResult.Value is not Goal prGoal)
        {
            errors.Add(new Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}"
            });
            return errors;
        }

        if (prGoal.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase))
            goal.MergeFrom(prGoal);

        return errors;
    }

    private static async Task ResolveGoalCallPaths(Actions actions, App.@this app,
        Actor.Context.@this context)
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
                        Path = Data.@this<FileSystem.Path>.Ok(FileSystem.Path.Resolve(expectedPrPath, context))
                    };
                    var existsResult = await app.RunAction(existsAction, context);
                    if (existsResult.Success && existsResult.Value is FileSystem.Path pathData && pathData.Exists)
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
