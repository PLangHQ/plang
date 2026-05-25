using System.Diagnostics;
using app.Utils;
using System.Text.Json;
using app.goals.goal;
using app.variables;
using Goal = app.goals.goal.@this;
using Actions = app.goals.goal.steps.step.actions.@this;

namespace app.modules.builder.code;

public class Default : IBuilder
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private readonly Stopwatch _buildTimer = new();

    // --- Actions ---

    public Task<data.@this> Actions(GetActions action)
    {
        var catalog = action.Context.App.Modules.Describe();

        // Optional filter: restrict the catalog to the named module.action
        // entries. The Compile step passes the planner's action set so the
        // prompt carries only the relevant rows. Null/empty → full catalog.
        var filter = action.Actions?.Value;
        if (filter is { Count: > 0 })
        {
            var wanted = new HashSet<string>(filter, StringComparer.OrdinalIgnoreCase);
            var subset = new StepActions();
            foreach (var a in catalog)
                if (wanted.Contains($"{a.Module}.{a.ActionName}"))
                    subset.Add(a);
            return Task.FromResult(data.@this.Ok(subset));
        }

        return Task.FromResult(data.@this.Ok(catalog));
    }

    // --- Types ---

    public data.@this Types(types action)
    {

        // The catalog is a structured object now — Build assembles primitives and
        // discovered record/enum entries. It pre-renders TypeNames/TypeSchemas for
        // the Liquid template, and keeps Types/PrimitiveNames for introspection
        // (JSON, UI, trace viewer).
        var modules = action.Context.App.Modules;
        var schema = modules.Schema.Build();

        // Optional Actions filter: restrict the Types list to entries actually
        // referenced by the named module.action set. Primitive types and the
        // entries renderer (TypeSchemas/TypeNames) all stay intact. Empty/null
        // filter → full catalog (back-compat).
        var filter = action.Actions?.Value;
        if (filter is { Count: > 0 })
        {
            var allTypeNames = new HashSet<string>(
                schema.Types.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tokenRx = new System.Text.RegularExpressions.Regex(@"\b[a-zA-Z][\w]*\b");
            var wantedActions = new HashSet<string>(filter, StringComparer.OrdinalIgnoreCase);

            // Walk every catalog-described action; for the ones in the filter,
            // collect type tokens from each parameter's rendered description and
            // from the return-type name. The Parameter.Value strings already
            // carry PLang type names (e.g. "path", "actor?", "%var% string"), so
            // tokenize on word boundaries and intersect with the type catalog.
            foreach (var a in modules.Describe())
            {
                if (!wantedActions.Contains($"{a.Module}.{a.ActionName}")) continue;
                foreach (var p in a.Parameters ?? new())
                {
                    var desc = p.Value as string ?? string.Empty;
                    foreach (System.Text.RegularExpressions.Match m in tokenRx.Matches(desc))
                        if (allTypeNames.Contains(m.Value)) refs.Add(m.Value);
                }
                if (!string.IsNullOrEmpty(a.ReturnTypeName)
                    && allTypeNames.Contains(a.ReturnTypeName))
                    refs.Add(a.ReturnTypeName);
            }

            // Transitive closure: types referenced by kept types should also be
            // kept (a record field might reference another record). The pass is
            // cheap — Types is in the dozens, and we only re-walk newly added
            // entries each iteration.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var t in schema.Types)
                {
                    if (!refs.Contains(t.Name)) continue;
                    var pieces = new[] { t.ConstructorSignature ?? "" };
                    foreach (var s in pieces)
                        foreach (System.Text.RegularExpressions.Match m in tokenRx.Matches(s))
                            if (allTypeNames.Contains(m.Value) && refs.Add(m.Value))
                                changed = true;
                    if (t.Fields != null)
                        foreach (var f in t.Fields)
                            if (allTypeNames.Contains(f.TypeName) && refs.Add(f.TypeName))
                                changed = true;
                    if (t.Properties != null)
                        foreach (var pr in t.Properties)
                            if (allTypeNames.Contains(pr.TypeName) && refs.Add(pr.TypeName))
                                changed = true;
                }
            }

            var filteredTypes = schema.Types.Where(t => refs.Contains(t.Name)).ToList();
            schema = new global::app.modules.Schema.@this(modules)
            {
                PrimitiveNames = schema.PrimitiveNames,
                Types = filteredTypes,
            };
        }

        return data.@this.Ok(schema);
    }

    // --- Goals ---

    public async Task<data.@this> Goals(goals action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var searchPath = string.IsNullOrWhiteSpace(action.Path.Value) ? "." : action.Path.Value!;

        // builder.goals.Path is project-root-relative ("the directory the user is
        // building"), not goal-relative. The runtime auto-seeds %path% to the
        // app root before Build.goal runs, so by the time we get here
        // action.Path.Value is typically already the absolute cwd. For the literal
        // "/" / "." / empty cases (no auto-seed), fall back to app.AbsolutePath.
        // For any other input that doesn't start with the root or with
        // "/", treat as a sibling-relative subpath under the root.
        var rootDir = app.AbsolutePath;
        string rootRelative;
        if (searchPath == "." || searchPath == "/" || searchPath == "\\")
            rootRelative = rootDir;
        else if (searchPath.StartsWith(rootDir, path.RootComparison))
            rootRelative = searchPath;  // already absolute under root — pass through
        else if (searchPath.StartsWith('/') || searchPath.StartsWith('\\'))
            rootRelative = searchPath;  // PLang-rooted, ValidatePath will anchor
        else
            rootRelative = global::System.IO.Path.GetFullPath(
                global::System.IO.Path.Combine(rootDir, searchPath));

        var listAction = new file.List
        {
            Context = context,
            Path = data.@this<path>.Ok(path.Resolve(rootRelative, context)),
            Pattern = new data.@this<string>("", "*.goal"),
            Recursive = new data.@this<bool>("", true)
        };
        var listResult = await app.RunAction(listAction, context);
        if (!listResult.Success)
            return listResult;

        var files = listResult.Value as List<path>;
        if (files == null || files.Count == 0)
            return data.@this.Ok(new List<Goal>());

        // Filter by app.Builder.Files if set (--build={"files":[...]})
        // Honor the user's specified order — building has bootstrapping concerns
        // (e.g., system/builder rebuilding itself: BuildGoal must come LAST so
        // earlier iterations use the previous in-memory build pipeline).
        var buildFiles = app.Builder.Files;
        if (buildFiles.Count > 0)
        {
            // Ensure filter paths have Context so FileName/Relative work
            foreach (var bf in buildFiles)
                bf.Context ??= context;

            bool MatchesPattern(path f, path bf)
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

            var ordered = new List<path>();
            var seen = new HashSet<string>();
            foreach (var bf in buildFiles)
            {
                foreach (var f in files)
                {
                    if (!MatchesPattern(f, bf)) continue;
                    if (seen.Add(f.Absolute)) ordered.Add(f);
                }
            }
            files = ordered;
            if (files.Count == 0)
                return data.@this.Ok(new List<Goal>());
        }

        var allGoals = new List<Goal>();
        var allErrors = new List<Info>();

        foreach (var file in files)
        {
            var readAction = new file.Read { Context = context, Path = data.@this<path>.Ok(file) };
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

            var goal = Goal.Parse(text, file);
            if (goal == null) continue;

            var mergeErrors = await MergePrData(goal, app, context);
            allErrors.AddRange(mergeErrors);

            allGoals.Add(goal);
        }

        _buildTimer.Restart();

        var result = data.@this.Ok(allGoals);
        if (allErrors.Count > 0)
            result.Warnings = allErrors;
        return result;
    }

    public async Task<data.@this> GoalsSave(goalsSave action)
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
        if (prPath == null)
            return data.@this.FromError(new errors.ActionError("Goal has no Path set, cannot derive PrPath", "NoPrPath", 400));

        // Group modifier actions onto their preceding executable action — recursive so
        // sub-goals are grouped too. Without this, sub-goal steps serialize with flat
        // modifiers and fail at runtime (a modifier's no-op Run wipes %!data%).
        goal.GroupModifiersRecursive(app.Modules);

        // Final safety net before persisting. Re-runs structural validation against the
        // goal's current Steps — catches any mismatch (step count, missing actions on
        // non-keep steps) that slipped past the in-pipeline validateResponse and
        // ApplyStep stages. Refusing to write the .pr is preferable to saving a half-
        // built artifact that the runtime can't execute.
        var validation = validateResponse.ValidateGoalState(goal);
        if (!validation.Success) return validation;

        var json = JsonSerializer.Serialize(goal, global::app.modules.builder.@this.PrWrite);

        var saveAction = new file.Save
        {
            Context = context,
            Path = data.@this<path>.Ok(prPath),
            Value = new data.@this("", json)
        };
        var saveResult = await app.RunAction(saveAction, context);

        var elapsed = _buildTimer.Elapsed;
        await app.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output,
            $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s){Environment.NewLine}");
        _buildTimer.Restart();

        return saveResult.Success ? data.@this.Ok(true) : saveResult;
    }

    // --- ValidateStepActions ---

    public data.@this ValidateStepActions(validateStepActions action)
    {
        var step = action.Step.Value!;
        var input = action.Actions.Value ?? new List<string>();
        var modules = action.Context.App.Modules;

        var result = new List<string>();

        // Validate the planner's suggestions — drop any that don't exist in the
        // runtime catalog. A hallucinated entry would feed the compiler a
        // non-resolving row and degrade into "missing-actions" anyway.
        foreach (var entry in input)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var parts = entry.Split('.', 2);
            if (parts.Length != 2) continue;
            if (!modules.Contains(parts[0], parts[1])) continue;
            result.Add(entry);
        }

        // Scan step text for explicit `<module>.<action>` tokens. Append any
        // that exist in the runtime catalog and aren't already in the set.
        // Append-only — the planner's order survives; explicit-mentions land
        // after. Word-boundary regex keeps false positives (%goal.Name%,
        // result.actions, dotted property paths) out — they get filtered
        // again by the catalog Contains check.
        foreach (var m in System.Text.RegularExpressions.Regex.Matches(
                     step.Text, @"\b([a-z][a-zA-Z0-9_]*)\.([a-z][a-zA-Z0-9_]*)\b")
                 .Cast<System.Text.RegularExpressions.Match>())
        {
            var mod = m.Groups[1].Value;
            var act = m.Groups[2].Value;
            if (!modules.Contains(mod, act)) continue;

            var key = $"{mod}.{act}";
            if (result.Any(s => s.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(key);
        }

        return data.@this.Ok(result);
    }

    // --- Validate ---

    public async Task<data.@this> Validate(validate action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var modules = app.Modules;

        if (action.Actions?.Value == null)
            return data.@this.Ok(true);

        var actions = action.Actions!.Value!;
        var notFound = new List<string>();
        foreach (var a in actions)
        {
            // Cheap repair for the recurring LLM hallucination of stuffing the
            // module/action separator into the module name:
            //   {"module": "ui.render",     "action": "render"} → module="ui",        action="render"
            //   {"module": "goal.call",     "action": "call"}   → module="goal",      action="call"
            //   {"module": "condition.if",  "action": "if"}     → module="condition", action="if"
            //   {"module": "loop.foreach",  "action": "foreach"} → module="loop",     action="foreach"
            // Both shapes appear: head.tail with action duplicating tail, and head.tail
            // with action being the genuine tail. Try both before reporting "not found"
            // so the fixer doesn't have to round-trip a deterministic mistake.
            if (!modules.Contains(a.Module, a.ActionName) && a.Module.Contains('.'))
            {
                var parts = a.Module.Split('.', 2);
                var head = parts[0];
                var tail = parts[1];
                if (modules.Contains(head, tail))
                {
                    a.Warnings.Add(new Info {
                        Key = "ModuleNameRepaired",
                        Message = $"Module name '{a.Module}' contained the action separator; repaired to module='{head}', action='{tail}' (was action='{a.ActionName}')."
                    });
                    a.Module = head;
                    a.ActionName = tail;
                }
                else if (modules.Contains(head, a.ActionName))
                {
                    a.Warnings.Add(new Info {
                        Key = "ModuleNameRepaired",
                        Message = $"Module name '{a.Module}' contained the action separator; repaired to module='{head}' (action='{a.ActionName}' kept)."
                    });
                    a.Module = head;
                }
            }
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
            return data.@this.FromError(new errors.ActionError(
                $"Actions not found: {string.Join("; ", notFound)}",
                "ActionNotFound", 400));
        }

        await ResolveGoalCallPaths(actions, app, context);
        var normalizationErrors = NormalizeParameterTypes(actions, modules, context);

        var validationErrors = new List<string>(normalizationErrors);
        foreach (var a in actions)
        {
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (a.Parameters != null)
                foreach (var p in a.Parameters) paramNames.Add(p.Name);
            a.Defaults = modules.GetDefaults(a.Module, a.ActionName, paramNames);

            // goal.call sanity — goal names are simple identifiers (BuildGoalCore,
            // HandleValidationError) or slash-paths (Setup/Init). They never contain
            // dots. The LLM occasionally hallucinates a CLR type name into the slot
            // (Fluid.Values.ObjectDictionaryFluidIndexable, App.GoalCall);
            // catch those here so LlmFixer retries instead of writing a dead .pr.
            if (a.Parameters != null)
            {
                foreach (var p in a.Parameters)
                {
                    if (!string.Equals(p.Type?.Value, "goal.call", StringComparison.OrdinalIgnoreCase)) continue;
                    // Catalog descriptions (e.g. "goal.call", "goal.call?") aren't real values —
                    // they're schema metadata from Modules.Describe(). Same skip as in
                    // NormalizeParameterTypes; without it, ToGoalCall parses "goal.call" as a
                    // dotted name and the type-name guard below false-positives on every
                    // goal.call slot in the catalog.
                    if (p.Value is string desc && IsCatalogDescription(desc, p.Type!.Value)) continue;
                    var goalCall = ToGoalCall(p.Value);
                    if (goalCall == null || string.IsNullOrEmpty(goalCall.Name)) continue;
                    if (goalCall.Name.Contains('%')) continue;  // %var% resolves at runtime
                    // Hard reject CLR type names — these are the known leak vector
                    // (Fluid template rendering a typed object via ToString()). A goal
                    // Name can never legitimately match a loaded CLR type's FullName.
                    if (app.Types.IsClrTypeName(goalCall.Name))
                        validationErrors.Add($"{a.Module}.{a.ActionName}: goal.call.Name '{goalCall.Name}' is a CLR type name. This is a build pipeline leak (likely a template rendering an object via ToString() instead of .Name). Use the actual goal name from the step text.");
                    else if (goalCall.Name.Contains('.'))
                        validationErrors.Add($"{a.Module}.{a.ActionName}: goal.call.Name '{goalCall.Name}' looks like a type name. Goal names are simple identifiers (e.g. 'BuildGoalCore', 'HandleValidationError'). Use the actual goal name from the @known mapping or the step text.");
                }
            }

            // Required-parameter check. A property is required when:
            //   - non-nullable type (Data<T>, not Data<T>?, not <T?>)
            //   - has no [Default] attribute
            //   - is not a [Code], capability interface, or framework slot
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
                        if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<modules.CodeAttribute>(prop) != null) continue;
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
            return data.@this.FromError(new errors.ActionError(
                string.Join("; ", validationErrors),
                "BuildValidation", 400));
        }

        return data.@this.Ok(true);
    }

    // --- Merge ---

    public data.@this Merge(merge action)
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
        return data.@this.Ok(action.Step.Value);
    }

    // --- Enrich Response ---

    public data.@this EnrichResponse(enrichResponse action)
    {

        var response = action.StepResults.Value;
        var goal = action.Goal.Value;
        if (response == null || goal == null)
            return data.@this.Ok(response);

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

        return data.@this.Ok(response);
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

    private static string RenderActionFormal(app.goals.goal.steps.step.actions.action.@this a)
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
        // InvariantCulture so locale-sensitive numeric output stays symmetric with
        // TypeConverter's InvariantCulture parse — see ExampleRenderer.cs for context.
        if (v is IConvertible conv) return System.Convert.ToString(conv, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        // Structured values (dicts, lists, POCOs like GoalCall) → JSON.
        try { return System.Text.Json.JsonSerializer.Serialize(v); }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException) { return v.ToString() ?? ""; }
    }

    // --- App ---

    public async Task<data.@this> Load(load action)
    {

        var app = action.Context.App;
        // App loads its identity from app.pr at Start() — just return it
        return data.@this.Ok(app);
    }

    public async Task<data.@this> AppSave(appSave action)
    {

        return await action.Context.App.Save();
    }

    // --- Promote Groups ---

    public async Task<data.@this> PromoteGroups(promoteGroups action)
    {

        var steps = ToStepList(action.Steps.Value);
        if (steps == null || steps.Count == 0)
            return data.@this.Ok(action.Steps.Value);

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
                if (!SetValue(step, "level", groupLevel))
                    return data.@this.FromError(new errors.ActionError(
                        $"PromoteGroups received a step as JsonElement (immutable) — expected IDictionary. " +
                        $"Step type: {step.GetType().FullName}. Group: '{group}'.",
                        "PromoteGroupsImmutableStep", 500));
                promoted++;
            }
        }

        if (promoted > 0)
            await action.Context.App.CurrentActor.Channels.WriteTextAsync(global::app.channels.@this.Output,
                $"  Group promotion: {promoted} step(s) promoted to detail pass{Environment.NewLine}");

        return data.@this.Ok(action.Steps.Value);
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

    /// <summary>
    /// Returns false if the step can't be mutated (e.g. immutable JsonElement),
    /// so the caller can surface a structured error instead of silently skipping.
    /// </summary>
    private static bool SetValue(object step, string key, string value)
    {
        if (step is IDictionary<string, object?> dict)
        {
            dict[key] = value;
            return true;
        }
        // JsonElement is immutable — caller decides how to surface this.
        return false;
    }

    /// <summary>
    /// Normalizes parameter values to match their declared type.
    /// LLMs are non-deterministic — they may produce "false" (string) instead of false (bool).
    /// This runs at build time so the .pr file has correct types.
    /// Returns conversion errors so the caller can fold them into validationErrors —
    /// without this, an LLM-emitted value that can't convert to the declared type
    /// would silently keep the wrong-typed value and the runtime would fail later.
    /// </summary>
    private static List<string> NormalizeParameterTypes(Actions actions, global::app.modules.@this modules,
        actor.context.@this context)
    {
        var errors = new List<string>();
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
                    var typeName = context.App.Types.GetTypeName(schemaProp.PropertyType);
                    if (typeName != "object")
                        p.Type = new data.type(typeName);
                }
            }

            foreach (var p in a.Parameters)
            {
                if (p.Value is null) continue;
                if (p.Value is string sv && sv.StartsWith('%') && sv.EndsWith('%')) continue; // variable reference
                if (p.Type == null) continue;

                // LLM-emitted "" for an unset nullable slot — same shape as
                // validateResponse's normalization, repeated here so detail-pass
                // results (which bypass validateResponse) also get the fix instead
                // of failing in TryConvertTo below. For non-nullable slots leave the
                // empty string in place so the conversion error surfaces and
                // LlmFixer retries.
                if (p.Value is string empty && empty.Length == 0
                    && global::app.modules.builder.ValidateResponseHelpers.IsNullableSchemaProp(actionType, p.Name))
                {
                    p.Value = null;
                    continue;
                }

                // Catalog descriptions ("int = 1", "%var% string", "list<int>?") are schema
                // metadata produced by Modules.Describe(), not values to normalize. They
                // surface when the catalog is fed back through validate (BuilderValidateValid
                // smoke test). Skip — coercing a description string to its declared type fails.
                if (p.Value is string desc && IsCatalogDescription(desc, p.Type.Value)) continue;

                var targetType = context.App.Types.Get(p.Type.Value);
                if (targetType == null) continue;

                // Scalar PlangType domain types (Path, etc.) carry their wire representation
                // AS the primitive — `Resolve(rawInput, context)` is the runtime constructor.
                // If we eagerly convert here, the saved .pr inflates the primitive into a
                // fully reflected record (Raw, Absolute, FileName, ...) that round-trips
                // poorly. Leave the primitive in the .pr; runtime auto-wraps via the source
                // generator's Resolve convention when the action actually executes.
                if (global::app.types.@this.IsScalarPlangType(targetType)) continue;

                // [Choices]-bearing types (Actor, Operator, ...) keep their string form in
                // the .pr — runtime resolves the chosen name via the type's own path
                // (App.GetActor, ctor registry, ...). Eagerly constructing here would
                // either fail (Actor has no usable string ctor) or produce a stateful
                // object that doesn't round-trip cleanly. Same shape as the scalar carve-out.
                if (context.App.Types.Choices.Has(targetType)) continue;

                // Already correctly typed? Skip (e.g. value is bool, target is bool).
                if (targetType.IsInstanceOfType(p.Value)) continue;

                // Convert in either direction: string → bool/int/double/etc., or
                // numeric/bool → string when the parameter is declared string. The LLM
                // emitting `Key=404 (int)` for a string-declared Key gets normalized here.
                var (converted, error) = global::app.types.@this.TryConvertTo(p.Value, targetType, context);
                if (converted != null)
                    p.Value = converted;
                else if (error != null)
                    errors.Add($"{a.Module}.{a.ActionName}.{p.Name}: {error.Message}");
            }
        }
        return errors;
    }

    /// <summary>
    /// Recognizes catalog description strings produced by <see cref="app.modules.@this.Describe"/>:
    /// the four forms <c>"X"</c>, <c>"X?"</c>, <c>"X = default"</c>, <c>"%var% X"</c> (and
    /// combinations). When the catalog itself is fed back through validate, every parameter's
    /// Value is one of these — coercing them through TypeMapping fails because they're
    /// metadata, not data. The match is anchored on <paramref name="typeName"/> (already
    /// stamped from the schema) so an LLM-emitted real value can't accidentally trip it.
    /// </summary>
    // internal-static for unit tests — the helper has 4 distinct match shapes and the
    // production callers only exercise the match-true path through integration tests.
    internal static bool IsCatalogDescription(string value, string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        var v = value.AsSpan().Trim();
        if (v.StartsWith("%var% ")) v = v[6..];
        if (!v.StartsWith(typeName)) return false;
        var rest = v[typeName.Length..];
        if (rest.Length == 0) return true;
        if (rest[0] == '?') rest = rest[1..];
        if (rest.Length == 0) return true;
        return rest.StartsWith(" = ");
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
    private static async Task<List<Info>> MergePrData(Goal goal, app.@this app,
        actor.context.@this context)
    {
        var errors = new List<Info>();
        var prPath = goal.PrPath;
        if (prPath == null) return errors;

        var readAction = new file.Read
        {
            Context = context,
            Path = data.@this<path>.Ok(prPath)
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

    private static async Task ResolveGoalCallPaths(Actions actions, app.@this app,
        actor.context.@this context)
    {
        foreach (var action in actions)
        {
            await ResolveGoalCallsInAction(action, app, context);

            // Modifiers (e.g. error.handle's `then call LogRetryError`) hold their own
            // goal.call parameters — same resolution rule applies.
            if (action.Modifiers != null)
            {
                foreach (var mod in action.Modifiers)
                    await ResolveGoalCallsInAction(mod, app, context);
            }
        }
    }

    private static async Task ResolveGoalCallsInAction(
        global::app.goals.goal.steps.step.actions.action.@this action,
        app.@this app, actor.context.@this context)
    {
        if (action.Parameters == null) return;

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

            // Ask the runtime to resolve the goal — same path/name lookup logic
            // GoalCall uses at dispatch. If it returns a Goal, copy that goal's
            // PrPath onto our GoalCall so the saved .pr carries an explicit path
            // (per "every goal.call should carry prPath" rule). Null result means
            // the goal couldn't be found — leave PrPath null, the validator's
            // downstream checks (or runtime) will surface a NotFound for it.
            goalCall.Action ??= action;
            var resolved = await goalCall.GetGoalAsync(app, context);
            if (resolved.Success && resolved.Value is Goal g && g.PrPath != null)
            {
                // Pre-resolve the .pr path. A slash-qualified Name keeps its
                // folder prefix in the saved .pr — LoadFromFile leaf-matches it
                // against the loaded goal's own (unqualified) Name at dispatch.
                goalCall.PrPath = g.PrPath;
            }

            param.Value = goalCall;
        }
    }

    private static GoalCall? ToGoalCall(object? value)
    {
        if (value is GoalCall gc) return gc;
        return global::app.types.@this.ConvertTo<GoalCall>(value);
    }
}
