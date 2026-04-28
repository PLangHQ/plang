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

    // --- Actions ---

    public Task<Data.@this> Actions(GetActions action)
    {

        return Task.FromResult(Data.@this.Ok(action.Context.App.Modules.Describe()));
    }

    // --- Types ---

    public Data.@this Types(types action)
    {

        // The catalog is a structured object now — Build assembles primitives and
        // discovered record/enum entries. It pre-renders TypeNames/TypeSchemas for
        // the Liquid template, and keeps Types/PrimitiveNames for introspection
        // (JSON, UI, trace viewer).
        return Data.@this.Ok(global::App.Catalog.@this.Build(action.Context.App.Modules));
    }

    // --- Goals ---

    public async Task<Data.@this> Goals(goals action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var searchPath = string.IsNullOrWhiteSpace(action.Path.Value) ? "." : action.Path.Value!;

        var listAction = new file.List
        {
            Context = context,
            Path = Data.@this<FileSystem.Path>.Ok(FileSystem.Path.Resolve(searchPath, context)),
            Pattern = new Data.@this<string>("", "*.goal"),
            Recursive = new Data.@this<bool>("", true)
        };
        var listResult = await app.RunAction(listAction, context);
        if (!listResult.Success)
            return listResult;

        var files = listResult.Value as FileSystem.Path[];
        if (files == null || files.Length == 0)
            return Data.@this.Ok(new List<Goal>());

        // Filter by app.Building.Files if set (--build={"files":[...]})
        // Honor the user's specified order — building has bootstrapping concerns
        // (e.g., system/builder rebuilding itself: BuildGoal must come LAST so
        // earlier iterations use the previous in-memory build pipeline).
        var buildFiles = app.Building.Files;
        if (buildFiles.Count > 0)
        {
            // Ensure filter paths have Context so FileName/Relative work
            foreach (var bf in buildFiles)
                bf.Context ??= context;

            bool MatchesPattern(FileSystem.Path f, FileSystem.Path bf)
            {
                // Use bf.Relative as the source of truth — bf.Raw is only set when a
                // Path is built via Path.Resolve, but TypeConverter constructs paths
                // via `new Path(string)` for JSON-derived filters, leaving Raw="".
                // Falling through to a bare-filename match in that case incorrectly
                // matched basenames across folders (e.g. "Errors/SimpleGoalCall/Start.goal"
                // would match every Start.goal in the tree).
                var bfRel = bf.Relative;
                var pathQualified = bfRel.Contains('/') || bfRel.Contains('\\');
                if (pathQualified)
                    return f.Relative.EndsWith(bfRel, StringComparison.OrdinalIgnoreCase)
                        || f.Relative.StartsWith(bfRel, StringComparison.OrdinalIgnoreCase);
                return f.FileName.Equals(bf.FileName, StringComparison.OrdinalIgnoreCase);
            }

            var ordered = new List<FileSystem.Path>();
            var seen = new HashSet<string>();
            foreach (var bf in buildFiles)
            {
                foreach (var f in files)
                {
                    if (!MatchesPattern(f, bf)) continue;
                    if (seen.Add(f.Absolute)) ordered.Add(f);
                }
            }
            files = ordered.ToArray();
            if (files.Length == 0)
                return Data.@this.Ok(new List<Goal>());
        }

        var allGoals = new List<Goal>();
        var allErrors = new List<Info>();

        foreach (var file in files)
        {
            var readAction = new file.Read { Context = context, Path = Data.@this<FileSystem.Path>.Ok(file) };
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

        var app = action.Context.App;
        var context = action.Context;
        var goal = action.Goal.Value!;

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

        // Group modifier actions onto their preceding executable action — recursive so
        // sub-goals are grouped too. Without this, sub-goal steps serialize with flat
        // modifiers and fail at runtime (a modifier's no-op Run wipes %__data__%).
        goal.GroupModifiersRecursive(app.Modules);

        // Final safety net before persisting. Re-runs structural validation against the
        // goal's current Steps — catches any mismatch (step count, missing actions on
        // non-keep steps) that slipped past the in-pipeline validateResponse and
        // ApplyStep stages. Refusing to write the .pr is preferable to saving a half-
        // built artifact that the runtime can't execute.
        var validation = validateResponse.ValidateGoalState(goal);
        if (!validation.Success) return validation;

        var json = JsonSerializer.Serialize(goal, Json.PrWrite);

        var saveAction = new file.Save
        {
            Context = context,
            Path = Data.@this<FileSystem.Path>.Ok(FileSystem.Path.Resolve(prPath, context)),
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

        var app = action.Context.App;
        var context = action.Context;
        var modules = app.Modules;

        if (action.Actions?.Value == null)
            return Data.@this.Ok(true);

        var actions = action.Actions!.Value!;
        var notFound = new List<string>();
        foreach (var a in actions)
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

        await ResolveGoalCallPaths(actions, app, context);
        NormalizeParameterTypes(actions, modules, context);

        var validationErrors = new List<string>();
        foreach (var a in actions)
        {
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (a.Parameters != null)
                foreach (var p in a.Parameters) paramNames.Add(p.Name);
            a.Defaults = modules.GetDefaults(a.Module, a.ActionName, paramNames);

            // goal.call sanity — goal names are simple identifiers (BuildGoalCore,
            // HandleValidationError) or slash-paths (Setup/Init). They never contain
            // dots. The LLM occasionally hallucinates a CLR type name into the slot
            // (Fluid.Values.ObjectDictionaryFluidIndexable, App.Goals.Goal.GoalCall);
            // catch those here so LlmFixer retries instead of writing a dead .pr.
            if (a.Parameters != null)
            {
                foreach (var p in a.Parameters)
                {
                    if (!string.Equals(p.Type?.Value, "goal.call", StringComparison.OrdinalIgnoreCase)) continue;
                    var goalCall = ToGoalCall(p.Value);
                    if (goalCall == null || string.IsNullOrEmpty(goalCall.Name)) continue;
                    if (goalCall.Name.Contains('%')) continue;  // %var% resolves at runtime
                    // Hard reject CLR type names — these are the known leak vector
                    // (Fluid template rendering a typed object via ToString()). A goal
                    // Name can never legitimately match a loaded CLR type's FullName.
                    if (Utils.PlangTypeIndex.IsClrTypeName(goalCall.Name))
                        validationErrors.Add($"{a.Module}.{a.ActionName}: goal.call.Name '{goalCall.Name}' is a CLR type name. This is a build pipeline leak (likely a template rendering an object via ToString() instead of .Name). Use the actual goal name from the step text.");
                    else if (goalCall.Name.Contains('.'))
                        validationErrors.Add($"{a.Module}.{a.ActionName}: goal.call.Name '{goalCall.Name}' looks like a type name. Goal names are simple identifiers (e.g. 'BuildGoalCore', 'HandleValidationError'). Use the actual goal name from the @known mapping or the step text.");
                }
            }

            // Required-parameter check. A property is required when:
            //   - non-nullable type (Data<T>, not Data<T>?, not <T?>)
            //   - has no [Default] attribute
            //   - is not a [Provider], capability interface, or framework slot
            // The LLM omitting a required param is a build-breaking mistake — without
            // the param, the runtime can't construct the action's parameter record.
            // Catch it at build time so LlmFixer / HandleValidationError can re-prompt.
            {
                var actType = modules.GetActionType(a.Module, a.ActionName);
                if (actType != null)
                {
                    var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (a.Parameters != null)
                        foreach (var p in a.Parameters) emitted.Add(p.Name);

                    var nullCtx = new System.Reflection.NullabilityInfoContext();
                    foreach (var prop in actType.GetProperties(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        if (prop.Name == "EqualityContract") continue;
                        if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<modules.ProviderAttribute>(prop) != null) continue;
                        if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<modules.DefaultAttribute>(prop) != null) continue;
                        if (CapabilityPropName(prop)) continue;

                        var nullable = System.Nullable.GetUnderlyingType(prop.PropertyType) != null
                            || (!prop.PropertyType.IsValueType
                                && nullCtx.Create(prop).WriteState == System.Reflection.NullabilityState.Nullable);
                        if (nullable) continue;

                        if (!emitted.Contains(prop.Name))
                            validationErrors.Add(
                                $"{a.Module}.{a.ActionName}: required parameter '{prop.Name}' is missing. " +
                                $"Every action must emit all non-nullable, non-default parameters.");
                    }
                }
            }

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

        // Diagnostic — gated by app.Debug.IsEnabled, drops on the floor in production.
        // The merge handoff was the spot a Boolean-vs-Step type mismatch surfaced during
        // the builder rebuild; leaving the line in earns its keep next time it drifts.
        var step = action.Step.Value;
        var from = action.StepFromLlm.Value;
        _ = action.Context.App.Debug.Write(
            $"builder.merge: step.Index={step?.Index} step.Actions={step?.Actions.Count} " +
            $"from.Index={from?.Index} from.Keep={from?.Keep} from.Actions={from?.Actions.Count}");

        action.Step.Value!.Merge(action.StepFromLlm.Value!);
        return Data.@this.Ok(action.Step.Value);
    }

    // --- Enrich Response ---

    public Data.@this EnrichResponse(enrichResponse action)
    {

        var response = action.StepResults.Value;
        var goal = action.Goal.Value;
        if (response == null || goal == null)
            return Data.@this.Ok(response);

        foreach (var step in response.Steps)
        {
            if (step.Index < 0 || step.Index >= goal.Steps.Count) continue;
            var prior = goal.Steps[step.Index];

            // LLM metadata backfill — for keep:true the LLM omits these to save
            // tokens; pull them off the prior so the trace viewer doesn't show
            // every cached step as 0% / level=null / no guidance.
            if (step.Guidance == null) step.Guidance = prior.Guidance;
            if (step.Level == null) step.Level = prior.Level;
            if (step.Confidence == null) step.Confidence = prior.Confidence;

            if (step.Keep)
            {
                // Copy the prior's actions onto the response step so the
                // downstream merge sees a fully populated Step.
                step.Actions.Clear();
                foreach (var a in prior.Actions) step.Actions.Add(a);
                if (string.IsNullOrEmpty(step.Formal))
                    step.Formal = RenderFormal(prior.Actions);
                step.Source = "known";
            }
            else if (prior.Actions.Count == 0)
            {
                step.Source = "new";
            }
            else if (prior.PriorText == prior.Text)
            {
                step.Source = "known";
            }
            else
            {
                step.Source = "hint";
            }
        }

        return Data.@this.Ok(response);
    }

    private static string RenderFormal(Actions actions)
    {
        var segments = new List<string>();
        foreach (var a in actions)
        {
            segments.Add(RenderActionFormal(a));
            foreach (var mod in a.Modifiers)
                segments.Add(RenderActionFormal(mod));
        }
        return string.Join(" | ", segments);
    }

    private static string RenderActionFormal(Goals.Goal.Steps.Step.Actions.Action.@this a)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(a.Module).Append('.').Append(a.ActionName);
        if (a.Parameters.Count > 0)
        {
            sb.Append(' ');
            for (int i = 0; i < a.Parameters.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var p = a.Parameters[i];
                sb.Append(p.Name).Append('(');
                if (p.Type != null) sb.Append('[').Append(p.Type.Value).Append("] ");
                sb.Append(FormatValue(p.Value));
                sb.Append(')');
            }
        }
        return sb.ToString();
    }

    private static string FormatValue(object? v)
    {
        if (v == null) return "null";
        if (v is string s) return s.Contains(' ') || s.Contains(',') ? $"\"{s}\"" : s;
        if (v is bool b) return b ? "true" : "false";
        if (v is IConvertible) return v.ToString() ?? "";
        // Structured values (dicts, lists, POCOs like GoalCall) → JSON.
        try { return System.Text.Json.JsonSerializer.Serialize(v); }
        catch { return v.ToString() ?? ""; }
    }

    // --- App ---

    public async Task<Data.@this> App(app action)
    {

        var app = action.Context.App;
        // App loads its identity from app.pr at Start() — just return it
        return Data.@this.Ok(app);
    }

    public async Task<Data.@this> AppSave(appSave action)
    {

        return await action.Context.App.Save();
    }

    // --- Promote Groups ---

    public Data.@this PromoteGroups(promoteGroups action)
    {

        var steps = ToStepList(action.Steps.Value);
        if (steps == null || steps.Count == 0)
            return Data.@this.Ok(action.Steps.Value);

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

        return Data.@this.Ok(action.Steps.Value);
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
        // JsonElement is immutable — log a warning since the value can't be set
        else if (step is JsonElement)
            Console.Error.WriteLine($"  Warning: Cannot set '{key}' on JsonElement step — expected IDictionary");
    }

    /// <summary>
    /// Normalizes parameter values to match their declared type.
    /// LLMs are non-deterministic — they may produce "false" (string) instead of false (bool).
    /// This runs at build time so the .pr file has correct types.
    /// </summary>
    private static void NormalizeParameterTypes(Actions actions, App.Modules.@this modules,
        Actor.Context.@this context)
    {
        foreach (var a in actions)
        {
            if (a.Parameters == null) continue;

            // Stamp types from the action schema, OVERRIDING any LLM-emitted type that
            // disagrees. The LLM tags the value's content shape (404 → "int"); the schema
            // tags the parameter's declared CLR type (Key → "string"). The schema wins —
            // it's the contract, not the LLM's view of the value.
            var actionType = modules.GetActionType(a.Module, a.ActionName);
            if (actionType != null)
            {
                var props = actionType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var p in a.Parameters)
                {
                    var schemaProp = props.FirstOrDefault(sp =>
                        string.Equals(sp.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (schemaProp == null) continue;
                    var typeName = TypeMapping.GetTypeName(schemaProp.PropertyType);
                    if (typeName != "object")
                        p.Type = new Data.Type(typeName);
                }
            }

            foreach (var p in a.Parameters)
            {
                if (p.Value is null) continue;
                if (p.Value is string sv && sv.StartsWith('%') && sv.EndsWith('%')) continue; // variable reference
                if (p.Type == null) continue;

                var targetType = TypeMapping.GetType(p.Type.Value);
                if (targetType == null) continue;

                // Scalar PlangType domain types (Path, etc.) carry their wire representation
                // AS the primitive — `Resolve(rawInput, context)` is the runtime constructor.
                // If we eagerly convert here, the saved .pr inflates the primitive into a
                // fully reflected record (Raw, Absolute, FileName, ...) that round-trips
                // poorly. Leave the primitive in the .pr; runtime auto-wraps via the source
                // generator's Resolve convention when the action actually executes.
                if (TypeMapping.IsScalarPlangType(targetType)) continue;

                // Already correctly typed? Skip (e.g. value is bool, target is bool).
                if (targetType.IsInstanceOfType(p.Value)) continue;

                // Convert in either direction: string → bool/int/double/etc., or
                // numeric/bool → string when the parameter is declared string. The LLM
                // emitting `Key=404 (int)` for a string-declared Key gets normalized here.
                var (converted, _) = TypeMapping.TryConvertTo(p.Value, targetType, context);
                if (converted != null)
                    p.Value = converted;
            }
        }
    }

    /// <summary>
    /// Capability-interface properties (Context, Step, Channels, Event, Static) are
    /// wired by the source generator from the execution context — they're not
    /// user-supplied parameters and the LLM never emits them. Skip them when
    /// computing the required-parameter set. Mirrors the filter in <c>Modules.Describe()</c>.
    /// </summary>
    private static bool CapabilityPropName(System.Reflection.PropertyInfo prop)
    {
        var declaring = prop.DeclaringType;
        if (declaring == null) return false;

        System.Type[] capabilityIfaces =
        [
            typeof(modules.IContext),
            typeof(modules.IStep),
            typeof(modules.IChannel),
            typeof(modules.IEvent),
            typeof(modules.IStatic),
        ];

        return capabilityIfaces.Any(iface =>
            iface.GetProperty(prop.Name) != null && iface.IsAssignableFrom(declaring));
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
            Path = Data.@this<FileSystem.Path>.Ok(FileSystem.Path.Resolve(prPath, context))
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
