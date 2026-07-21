using System.Diagnostics;
using app.Utils;
using System.Text.Json;
using app.goal;
using app.variable;
using Goal = app.goal.@this;
using Actions = System.Collections.Generic.List<app.goal.step.action.@this>;

namespace app.module.action.build.code;

public class Default : IBuilder
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private readonly Stopwatch _buildTimer = new();

    // --- Actions ---

    public async Task<data.@this> Actions(GetActions action)
    {
        var catalog = await action.Context.App.Module.Describe();

        // Optional filter: restrict the catalog to the named module.action
        // entries. The Compile step passes the planner's action set so the
        // prompt carries only the relevant rows. Null/empty → full catalog.
        var filter = action.Actions == null || await action.Actions.IsEmpty() ? null
            : (await action.Actions.Value()).Clr<List<string>>();
        if (filter is { Count: > 0 })
        {
            var wanted = new HashSet<string>(filter, StringComparer.OrdinalIgnoreCase);
            var subset = new List<global::app.goal.step.action.@this>();
            foreach (var a in catalog)
                if (wanted.Contains($"{a.Module}.{a.ActionName}"))
                    subset.Add(a);
            return action.Context.Ok(new global::app.type.clr.@this<List<global::app.goal.step.action.@this>>(subset, action.Context));
        }

        // The catalog rides as clr<List<action>> (the Run signature carries that), so the consumer
        // unwraps one shape, matching the goal/step/action graph.
        return action.Context.Ok(new global::app.type.clr.@this<List<global::app.goal.step.action.@this>>(catalog, action.Context));
    }

    // --- Types ---

    public async Task<data.@this> Types(types action)
    {

        // The catalog is a structured object now — Build assembles primitives and
        // discovered record/enum entries. It pre-renders TypeNames/TypeSchemas for
        // the Liquid template, and keeps Types/PrimitiveNames for introspection
        // (JSON, UI, trace viewer).
        var modules = action.Context.App.Module;
        var schema = modules.Schema.Build();

        // Optional Actions filter: restrict the Types list to entries actually
        // referenced by the named module.action set. Primitive types and the
        // entries renderer (TypeSchemas/TypeNames) all stay intact. Empty/null
        // filter → full catalog (back-compat).
        var filter = action.Actions == null || await action.Actions.IsEmpty() ? null
            : (await action.Actions.Value()).Clr<List<string>>();
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
            foreach (var a in await modules.Describe())
            {
                if (!wantedActions.Contains($"{a.Module}.{a.ActionName}")) continue;
                foreach (var p in a.Parameter ?? new())
                {
                    var desc = ((await p.Value()) as global::app.type.item.text.@this)?.Clr<string>() ?? string.Empty;
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
            schema = new global::app.type.list.view.@this(modules)
            {
                PrimitiveNames = schema.PrimitiveNames,
                Types = filteredTypes,
                Kinds = schema.Kinds,
            };
        }

        return action.Context.Ok(schema);
    }

    // --- Goals ---

    public async Task<data.@this> Goals(goals action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var searchPathValue = (await action.Path.Value())?.ToString();
        var searchPath = string.IsNullOrWhiteSpace(searchPathValue) ? "." : searchPathValue!;

        // builder.goals.Path is project-root-relative ("the directory the user is
        // building"), not goal-relative. The runtime auto-seeds %path% to the
        // app root before Build.goal runs, so by the time we get here
        // (await action.Path.Value()) is typically already the absolute cwd. For the literal
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
            // Lift to path verbs — Resolve handles normalization, no
            // System.IO.Path arithmetic needed.
            rootRelative = global::app.type.item.path.@this.Resolve(searchPath, context).Absolute;

        var listAction = new file.List(context)
        {
            Path = context.Ok<path>(path.Resolve(rootRelative, context)),
            Pattern = new data.@this<global::app.type.item.text.@this>("", "*.goal", context: context),
            Recursive = new data.@this<global::app.type.item.@bool.@this>("", true, context: context)
        };
        var listResult = await app.Run(listAction, context);
        if (!listResult.Success)
            return listResult;

        var files = (await listResult.Value()).Clr<List<path>>();
        if (files == null || files.Count == 0)
            return context.Ok(new List<Goal>());

        // Filter by app.Build.Files if set (--build={"files":[...]})
        // Honor the user's specified order — building has bootstrapping concerns
        // (e.g., system/builder rebuilding itself: BuildGoal must come LAST so
        // earlier iterations use the previous in-memory build pipeline).
        // Each row lifts to a path at ITS door — a JSON-string row becomes a path (path.Create), a
        // %var% row resolves the variable. This is the materialize-on-read the plang-typed Build.Files
        // buys: the walk stored the list lazily, the consumer opens each row here.
        var filters = new List<path>();
        foreach (var row in app.Build.Files)
            if (await row.Value<global::app.type.item.path.@this>() is { } bf)
            { bf.Context ??= context; filters.Add(bf); }

        if (filters.Count > 0)
        {
            // The affix/filename filter semantics live on path (path.Matches) —
            // the type owns its containment math.
            bool MatchesPattern(path f, path bf) => f.Matches(bf).Value;

            var ordered = new List<path>();
            var seen = new HashSet<string>();
            foreach (var bf in filters)
            {
                foreach (var f in files)
                {
                    if (!MatchesPattern(f, bf)) continue;
                    if (seen.Add(f.Absolute)) ordered.Add(f);
                }
            }
            files = ordered;
            if (files.Count == 0)
                return context.Ok(new List<Goal>());
        }

        var allGoals = new List<Goal>();
        var allErrors = new List<Info>();

        foreach (var file in files)
        {
            var readAction = new file.Read(context) { Path = context.Ok<path>(file) };
            var readResult = await app.Run(readAction, context);
            if (!readResult.Success)
            {
                allErrors.Add(new Info
                {
                    Key = "FileReadError",
                    Message = $"Failed to read {file.Raw}: {readResult.Error?.Message}"
                });
                continue;
            }

            var text = (await readResult.Value())?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var goal = Goal.Parse(text, file);
            if (goal == null) continue;

            var mergeErrors = await MergePrData(goal, app, context);
            allErrors.AddRange(mergeErrors);

            allGoals.Add(goal);
        }

        _buildTimer.Restart();

        var result = context.Ok(allGoals);
        if (allErrors.Count > 0)
            result.Warnings = allErrors;
        return result;
    }

    // --- Fold: indent-authored sub-steps → gate-action Child ---

    public Task<data.@this> Fold(fold action)
    {
        var context = action.Context;
        var goal = action.Goal.Clr<Goal>()!;
        var errors = new List<global::app.error.IError>();
        Fold(goal, errors);
        if (errors.Count == 0) return Task.FromResult(context.Ok(true));

        // Surface every A4 violation: the first is the root, the rest ride its ErrorChain —
        // each error carries its own offending step (location), not a flattened string.
        var root = errors[0];
        for (int e = 1; e < errors.Count; e++) root.ErrorChain.Add(errors[e]);
        return Task.FromResult(context.Error(root));
    }

    // Folds a goal's own steps, then recurses its sub-goals. Sets each goal's Step to the
    // nested projection — the goal owns its (now-tree) step collection.
    private void Fold(Goal goal, List<global::app.error.IError> errors)
    {
        goal.Step = new global::app.goal.step.list.@this(Fold(goal.Step.list, errors));
        foreach (var subGoal in goal.Goals) Fold(subGoal, errors);
    }

    // Flat + Indent → tree: a step's deeper-indented followers move into that step's gate
    // action (the IsCondition action) Child; recursion composes nested blocks. A block under
    // a non-condition step is an authoring error (A4) — recorded against the offending step,
    // never silently dropped or kept flat. Real steps only; nothing is synthesized here.
    private List<global::app.goal.step.@this> Fold(
        IReadOnlyList<global::app.goal.step.@this> flat, List<global::app.error.IError> errors)
    {
        var top = new List<global::app.goal.step.@this>();
        int i = 0;
        while (i < flat.Count)
        {
            var step = flat[i];
            int j = i + 1;
            while (j < flat.Count && flat[j].Indent > step.Indent) j++;   // gather the deeper block

            if (j > i + 1)
            {
                var block = new List<global::app.goal.step.@this>();
                for (int k = i + 1; k < j; k++) block.Add(flat[k]);

                var gate = System.Linq.Enumerable.FirstOrDefault(step.Action.list, a => a.IsCondition);
                if (gate == null)
                    errors.Add(new global::app.error.StepError(
                        $"indented steps under non-condition step '{step.Text}'",
                        step, "IndentUnderNonCondition", 400));
                else
                    gate.Child = new global::app.goal.step.list.@this(Fold(block, errors));
            }
            top.Add(step);   // the real step keeps its identity at this level
            i = j;
        }
        return top;
    }

    public async Task<data.@this> GoalsSave(goalsSave action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var goal = action.Goal.Clr<Goal>()!;

        // Apply LLM-generated description if available in Variables
        var stepResults = await context.Variable.Get("stepResults");
        if ((await stepResults.Value()) is IDictionary<string, object?> resultsDict
            && resultsDict.TryGetValue("description", out var desc)
            && desc is string description
            && !string.IsNullOrEmpty(description))
        {
            goal.Description = description;
        }

        var prPath = goal.PrPath;
        if (prPath == null)
            return context.Error(new global::app.error.ActionError("Goal has no Path set, cannot derive PrPath", "NoPrPath", 400));

        // Group modifier actions onto their preceding executable action — recursive so
        // sub-goals are grouped too. Without this, sub-goal steps serialize with flat
        // modifiers and fail at runtime (a modifier's no-op Run wipes %!data%).
        goal.NestRecursive(app.Module);

        // Final safety net before persisting. Re-runs structural validation against the
        // goal's current Steps — catches any mismatch (step count, missing actions on
        // non-keep steps) that slipped past the in-pipeline validateResponse and
        // ApplyStep stages. Refusing to write the .pr is preferable to saving a half-
        // built artifact that the runtime can't execute.
        // The builder is self-hosted: context.App is the BUILDER's own app, not the app being
        // built. The goal being saved belongs to the TARGET app, passed in as the App parameter
        // (`build.goalsSave Goal=%goal%, App=%app%`) — the runtime never guesses a plang variable
        // name. Validate resolves the goal's types/valid-values against it; fall back to
        // context.App only when App is absent (isolated tests that save a hand-built goal).
        var targetApp = (action.App == null ? null : (await action.App.Value())?.Value)
            ?? throw new global::app.error.AppException(
                "build.goalsSave requires the target App — call it as `build.goalsSave Goal=%goal%, App=%app%`. "
                + "The self-hosted builder's own context.App is a DIFFERENT app; validating the built goal against it is wrong.",
                "MissingApp", 500);
        var validation = await BuildResponse.FromGoalState(goal).Validate(goal, targetApp);
        if (!validation.Success) return validation;


        // The goal writes its OWN .pr through Output (the value-owns-serialization path), Store view,
        // structural (no @schema — a param is a Data by its List<Data> position). Symmetric with the
        // goal reader's bare read. Replaces STJ + PrWrite + the WireLocal/Normalize write track.
        var serializer = (global::app.channel.serializer.plang.@this)
            context.Actor.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, goal, global::app.View.Store);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        var saveAction = new file.Save(context)
        {
            Path = context.Ok<path>(prPath),
            Value = new data.@this("", json, context: context)
        };
        var saveResult = await app.Run(saveAction, context);

        var elapsed = _buildTimer.Elapsed;
        await context.Actor.Channel.WriteTextAsync(global::app.channel.list.@this.Output,
            $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s){Environment.NewLine}");
        _buildTimer.Restart();

        return saveResult.Success ? context.Ok(true) : saveResult;
    }

    // --- ValidateStepActions ---

    public async System.Threading.Tasks.Task<data.@this> ValidateStepActions(validateStepActions action)
    {
        var step = await action.Step.Value();
        if (step == null)
        {
            // Dump what the planner actually returned so the user can see the
            // malformed shape instead of guessing. Skip system/user/usage —
            // those are stamped by the parent goal (input we sent + cost
            // bookkeeping), not the LLM's planning output.
            string planDetail = "(no plan was produced)";
            try
            {
                var planValue = action.Context.Variable.Peek("plan")?.Peek();
                if (planValue != null)
                {
                    // Round-trip whatever shape the planner produced
                    // (JsonElement, JsonNode, Dictionary, anonymous record) into
                    // a JsonNode so we can extract just description + steps.
                    var raw = System.Text.Json.JsonSerializer.Serialize(planValue);
                    var node = System.Text.Json.Nodes.JsonNode.Parse(raw);
                    var steps = node?["steps"];
                    if (steps != null)
                    {
                        var preview = new System.Text.Json.Nodes.JsonObject
                        {
                            ["description"] = node!["description"]?.DeepClone(),
                            ["steps"] = steps.DeepClone(),
                        };
                        planDetail = "the LLM returned this plan:\n" +
                            preview.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    }
                    else
                    {
                        planDetail = "the LLM never returned a steps array — usually means the response failed to parse or the retry chain exhausted before any valid plan came back.";
                    }
                }
            }
            catch (System.Exception) { /* fall through with the default planDetail */ }

            return action.Context.Error(new global::app.error.ActionError(
                "The LLM couldn't produce a usable plan for this goal — its proposed step count didn't match the goal, and the retry didn't recover. " +
                "Try running plang build again (the LLM is non-deterministic). " +
                "If it keeps failing, simplify or reword your goal text — long quoted strings or phrases that look like instructions can confuse the planner.\n\n" +
                $"What we got back: {planDetail}",
                "BuilderPlannerFailed", 400));
        }
        // Sync build surface — read the in-memory backing (the planner's list
        // is authored in this process; no door to open).
        var input = (await action.Actions.Value()).Clr<List<string>>() ?? new List<string>();
        var modules = action.Context.App.Module;

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

        return action.Context.Ok(result);
    }

    // --- Validate ---

    public async Task<data.@this> Validate(validate action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var modules = app.Module;

        if ((action.Actions == null ? null : await action.Actions.Value()) == null)
            return context.Ok(true);

        var actions = action.Actions.Clr<List<global::app.goal.step.action.@this>>()!;
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
            return context.Error(new global::app.error.ActionError(
                $"Actions not found: {string.Join("; ", notFound)}",
                "ActionNotFound", 400));
        }

        await ResolveGoalCallPaths(actions, app, context);
        var normalizationErrors = NormalizeParameterTypes(actions, modules, context);

        var validationErrors = new List<string>(normalizationErrors);
        foreach (var a in actions)
        {
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (a.Parameter != null)
                foreach (var p in a.Parameter) paramNames.Add(p.Name);
            a.Default = modules.GetDefaults(a.Module, a.ActionName, paramNames) is { } defs
                ? new global::app.goal.step.action.parameter.list.@this(defs) : null;

            // goal.call sanity — goal names are simple identifiers (BuildGoalCore,
            // HandleValidationError) or slash-paths (Setup/Init). They never contain
            // dots. The LLM occasionally hallucinates a CLR type name into the slot
            // (Fluid.Values.ObjectDictionaryFluidIndexable, App.GoalCall);
            // catch those here so LlmFixer retries instead of writing a dead .pr.
            if (a.Parameter != null)
            {
                foreach (var p in a.Parameter)
                {
                    if (!string.Equals(p.Type?.Name, "goal.call", StringComparison.OrdinalIgnoreCase)) continue;
                    // Catalog descriptions (e.g. "goal.call", "goal.call?") aren't real values —
                    // they're schema metadata from Modules.Describe(). Same skip as in
                    // NormalizeParameterTypes; without it, ToGoalCall parses "goal.call" as a
                    // dotted name and the type-name guard below false-positives on every
                    // goal.call slot in the catalog.
                    if ((await p.Value()) is global::app.type.item.text.@this descText
                        && IsCatalogDescription(descText, p.Type!.Name)) continue;
                    var goalCall = ToGoalCall((await p.Value()), context);
                    if (goalCall == null || string.IsNullOrEmpty(goalCall.Name)) continue;
                    if (goalCall.Name.Contains('%')) continue;  // %var% resolves at runtime
                    // Hard reject CLR type names — these are the known leak vector
                    // (Fluid template rendering a typed object via ToString()). A goal
                    // Name can never legitimately match a loaded CLR type's FullName.
                    if (app.Type.IsClrTypeName(goalCall.Name))
                        validationErrors.Add($"{a.Module}.{a.ActionName}: goal.call.Name '{goalCall.Name}' is a CLR type name. This is a build pipeline leak (likely a template rendering an object via ToString() instead of .Name). Use the actual goal name from the step text.");
                    else if (goalCall.Name.Contains('.'))
                    {
                        // Repair the recurring LLM leak of stuffing the formal goal.call
                        // notation into the goal NAME itself — e.g. event.on's GoalToCall
                        // coming back as "goal.call(LogBefore)" / "goal.call LogBefore".
                        // The real name is the inner identifier. Repair + warn rather than
                        // reject: rejecting triggers a FixValidation retry that tends to
                        // DEGRADE (nano returns prose in `formal` and a bare `goal` param,
                        // dropping the required Trigger → "trigger must have a value" at
                        // runtime). Mirrors the module-name-separator repair above.
                        var m = System.Text.RegularExpressions.Regex.Match(
                            goalCall.Name, @"^goal\.call\s*\(?\s*([A-Za-z_][\w/]*)\s*\)?$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            a.Warnings.Add(new Info {
                                Key = "GoalCallNameRepaired",
                                Message = $"goal.call.Name '{goalCall.Name}' carried the formal goal.call notation; repaired to '{m.Groups[1].Value}'."
                            });
                            // Name is init-only; rebuild with the repaired name, carry the rest.
                            p.SetValue(new GoalCall {
                                Name = m.Groups[1].Value,
                                Parallel = goalCall.Parallel,
                                Parameters = goalCall.Parameters,
                                PrPath = goalCall.PrPath,
                            });
                        }
                        else
                            validationErrors.Add($"{a.Module}.{a.ActionName}: goal.call.Name '{goalCall.Name}' looks like a type name. Goal names are simple identifiers (e.g. 'BuildGoalCore', 'HandleValidationError'). Use the actual goal name from the @known mapping or the step text.");
                    }
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
                // Read the ONE reflection site — the catalog element's declared parameter rows —
                // instead of re-reflecting the handler with a fresh NullabilityInfoContext. The rows
                // already drop [Code] / capability / EqualityContract / host params; a required slot
                // is a row that's non-nullable with no [Default].
                var element = modules.Contains(a.Module, a.ActionName)
                    ? modules[a.Module][a.ActionName]
                    : null;
                if (element != null)
                {
                    var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (a.Parameter != null)
                        foreach (var p in a.Parameter) emitted.Add(p.Name);

                    foreach (var row in element.ParameterRows)
                    {
                        if (row.Nullable || row.Default != null) continue;
                        if (!emitted.Contains(row.Name))
                            validationErrors.Add(
                                $"{a.Module}.{a.ActionName}: required parameter '{row.Name}' is missing. " +
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
                    var error = (string?)method.Invoke(null, [a.Parameter.ToList()]);
                    if (error != null)
                        validationErrors.Add($"{a.Module}.{a.ActionName}: {error}");
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            return context.Error(new global::app.error.ActionError(
                string.Join("; ", validationErrors),
                "BuildValidation", 400));
        }

        // Per-action Build() pass — each handler may stamp a type on the step's
        // terminal variable.set. See IClass.Build for the contract.
        var buildErrors = await RunBuildPass(actions, modules, context);
        if (buildErrors.Count > 0)
        {
            return context.Error(new global::app.error.ActionError(
                string.Join("; ", buildErrors),
                "BuildValidation", 400));
        }

        return context.Ok(true);
    }

    /// <summary>
    /// Walks each action's IClass.Build() — Build is the compile-time hook that lets
    /// a handler infer a Type for the step's terminal variable.set from its own
    /// parameters (file.read on a literal .csv → "csv", llm.query with a schema →
    /// "json"). A returned typeName stamps onto the terminal variable.set's "Type"
    /// parameter; Fail aborts validation; bare Ok contributes nothing.
    /// </summary>
    internal static async Task<List<string>> RunBuildPass(Actions actions, global::app.module.list.@this modules,
        actor.context.@this context)
    {
        var errors = new List<string>();
        foreach (var a in actions)
        {
            var (shell, _) = modules.GetCodeGenerated(a, context);
            if (shell == null) continue;
            // Resolve builds a populated instance (params decoded); Build() reads them.
            var (handler, resolveErr) = await shell.Resolve(a, context);
            if (resolveErr != null)
            {
                errors.Add($"{a.Module}.{a.ActionName}: {resolveErr.Message}");
                break;
            }
            if (handler is not global::app.module.IClass classified) continue;
            var buildResult = await classified.Build();
            if (!buildResult.Success)
            {
                errors.Add($"{a.Module}.{a.ActionName}: {buildResult.Error?.Message ?? "Build() failed"}");
                break;
            }
            // Publish this action's Build() result as %!buildData% — the handle the
            // NEXT action's Build() reads to see what it captures (mirrors runtime's
            // %!data%, but build-scoped so it can't clobber the runtime %!data% the
            // System actor uses while the builder runs). The pass stays generic: it
            // never special-cases variable.set; each handler decides whether to use
            // %!buildData% (variable.set.Build does).
            await context.Variable.Set("!buildData", buildResult);
        }
        return errors;
    }

    // --- Merge ---

    public async Task<data.@this> Merge(merge action)
    {

        // Diagnostic — gated by app.Debug presence (null = off), drops on the floor in production.
        // The merge handoff was the spot a Boolean-vs-Step type mismatch surfaced during
        // the builder rebuild; leaving the line in earns its keep next time it drifts.
        var step = await action.Step.Value();
        var from = await action.StepFromLlm.Value();
        _ = action.Context.App.Debug?.Write(
            $"builder.merge: step.Index={step?.Index} step.Action={step?.Action.Count} " +
            $"from.Index={from?.Index} from.Keep={from?.Keep} from.Action={from?.Action.Count}");

        step!.Merge(from!);
        return action.Context.Ok(step);
    }

    // --- Enrich Response ---

    public async Task<data.@this> EnrichResponse(enrichResponse action)
    {

        var response = action.StepResults.Peek() as BuildResponse;
        var goal = action.Goal.Clr<Goal>();
        if (response == null || goal == null)
            return action.Context.Ok(response);

        foreach (var step in response.Steps)
        {
            if (step.Index < 0 || step.Index >= goal.Step.Count) continue;
            var prior = goal.Step[step.Index];

            if (step.Keep)
            {
                // Copy the prior's actions onto the response step so the
                // downstream merge sees a fully populated Step.
                step.Action = new global::app.goal.step.action.list.@this(prior.Action.list);
                if (string.IsNullOrEmpty(step.Formal))
                    step.Formal = await RenderFormal(prior.Action.list, action.Context);
                step.Source = "known";
            }
            else if (prior.Action.Count == 0)
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

        return action.Context.Ok(response);
    }

    // The formal line (module.action Name([type] value), … | modifier…) is rendered by the
    // actionFormal template, not C#: the actions become a self-describing plang value model, each
    // param value writes ITSELF via the template's `| formal` filter (the value's own text.Writer —
    // no STJ, no [JsonConverter]). This is the backfill for a reused step whose LLM `formal` is empty.
    private async Task<string> RenderFormal(System.Collections.Generic.IReadOnlyList<global::app.goal.step.action.@this> actions, global::app.actor.context.@this ctx)
    {
        var model = new global::app.type.item.list.@this(ctx);
        foreach (var a in actions) model.Add(ActionModel(a, ctx));

        var parameters = new global::app.type.item.list.@this(ctx);
        parameters.Add(new data.@this("actions", model, context: ctx));

        var render = new global::app.module.action.ui.Render(ctx)
        {
            Template = new data.@this<global::app.type.item.text.@this>("", "/system/builder/templates/actionFormal.template", context: ctx),
            IsFile = new data.@this<global::app.type.item.@bool.@this>("", true, context: ctx),
            Parameters = ctx.Ok<global::app.type.item.list.@this>(parameters),
        };
        var result = await ctx.App.Run(render, ctx);
        if (!result.Success)
            throw new global::app.error.AppException(
                $"Failed to render the actionFormal template: {result.Error?.Message}", "FormalRenderFailed", 500);
        return (await result.Value())?.ToString()?.Trim() ?? "";
    }

    // An action (or modifier — a modifier IS an action) as a navigable dict the template loops.
    private global::app.type.item.dict.@this ActionModel(app.goal.step.action.@this a, global::app.actor.context.@this ctx)
    {
        var d = new global::app.type.item.dict.@this(ctx);
        d.Set("Module", a.Module);
        d.Set("ActionName", a.ActionName);
        var ps = new global::app.type.item.list.@this(ctx);
        foreach (var p in a.Parameter) ps.Add(ParamModel(p, ctx));
        d.Set("Parameters", ps);
        var mods = new global::app.type.item.list.@this(ctx);
        foreach (var m in a.Modifiers) mods.Add(ActionModel(m, ctx));
        d.Set("Modifiers", mods);
        return d;
    }

    private global::app.type.item.dict.@this ParamModel(data.@this p, global::app.actor.context.@this ctx)
    {
        var d = new global::app.type.item.dict.@this(ctx);
        d.Set("Name", p.Name);
        if (!string.IsNullOrEmpty(p.Type.Name)) d.Set("Type", p.Type.Name);
        d.Set("Value", p.Peek());   // the value writes itself in the template via | formal
        return d;
    }

    // --- App ---

    public async Task<data.@this> Load(load action)
    {

        var app = action.Context.App;
        // App loads its identity from app.pr at Start() — just return it
        return action.Context.Ok(app);
    }

    public async Task<data.@this> AppSave(appSave action)
    {

        return await action.Context.App.Save();
    }

    // --- Promote Groups ---

    public async Task<data.@this> PromoteGroups(promoteGroups action)
    {

        var steps = ToStepList((await action.Steps.Value()));
        if (steps == null || steps.Count == 0)
            return action.Context.Ok((await action.Steps.Value()));

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
                    return action.Context.Error(new global::app.error.ActionError(
                        $"PromoteGroups received a step as JsonElement (immutable) — expected IDictionary. " +
                        $"Step type: {step.GetType().FullName}. Group: '{group}'.",
                        "PromoteGroupsImmutableStep", 500));
                promoted++;
            }
        }

        if (promoted > 0)
            await action.Context.Actor.Channel.WriteTextAsync(global::app.channel.list.@this.Output,
                $"  Group promotion: {promoted} step(s) promoted to detail pass{Environment.NewLine}");

        return action.Context.Ok((await action.Steps.Value()));
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
        // The steps value is the native list type now — read each element's value.
        if (steps is app.type.item.list.@this nativeList)
            return nativeList.Items.Select(d => (object?)d.Peek()).Where(v => v != null).Select(v => v!).ToList();
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
    internal static List<string> NormalizeParameterTypes(System.Collections.Generic.IReadOnlyList<global::app.goal.step.action.@this> actions, global::app.module.list.@this modules,
        actor.context.@this context)
    {
        var errors = new List<string>();
        foreach (var a in actions)
        {
            if (a.Parameter == null) continue;

            // Stamp types from the action schema, OVERRIDING any LLM-emitted type that
            // disagrees. The LLM tags the value's content shape (404 → "int"); the schema
            // tags the parameter's declared CLR type (Key → "string"). The schema wins —
            // it's the contract, not the LLM's view of the value.
            var actionType = modules.GetActionType(a.Module, a.ActionName);
            // The catalog element's declared rows — the ONE reflection site, read for nullable-slot
            // detection below instead of re-reflecting with a NullabilityInfoContext.
            var rows = modules.Contains(a.Module, a.ActionName)
                ? modules[a.Module][a.ActionName].ParameterRows : null;
            if (actionType != null)
            {
                var props = actionType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var p in a.Parameter)
                {
                    var schemaProp = props.FirstOrDefault(sp =>
                        string.Equals(sp.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (schemaProp == null) continue;
                    var typeName = context.App.Type.GetTypeName(schemaProp.PropertyType);
                    if (typeName != "object")
                        p.Declare(new app.type.@this(typeName));

                    // plang-types: stamp kind alongside type when the declared
                    // type carries a static Build(value) hook. Separate field
                    // on the .pr — never "type:kind". Skip variable refs
                    // (%var% values resolve at runtime); an authored string
                    // rides as text and presents its string face here.
                    var sv = p.Peek() as global::app.type.item.text.@this;
                    if (p.Peek() is not null && !(sv != null && sv.StartsWith("%") && sv.EndsWith("%")))
                    {
                        var declared = schemaProp.PropertyType;
                        var underlying = System.Nullable.GetUnderlyingType(declared) ?? declared;
                        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
                            underlying = underlying.GetGenericArguments()[0];
                        // Build through the family's eager door and stamp the param with the built
                        // value's OWN type descriptor ({name, kind}) — one construction door (image
                        // parses its path extension → jpg, number reads the literal's precision → int).
                        // A decline (null, or an error on the throwaway carrier) → no stamp; never
                        // fail the build over a kind probe.
                        // `underlying` is the DECLARED param CLR type — an identity lookup ("what plang
                        // type IS this"). The indexer is never-null; a POCO param answers the clr entity,
                        // which wraps the authored value in a generic carrier (kind `*`). That is not a
                        // real kind refinement — only a value with its OWN item type (image → jpg, number
                        // → int) stamps; a clr carrier leaves the param on its declared type.
                        var entity = context.App.Type[underlying];
                        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(entity.Name), context: context);
                        if (entity.Create(p.Peek(), carrier) is { Type.Kind: not null } built
                            && built is not global::app.type.clr.@this)
                            p.Declare(built.Type);
                    }
                }
            }

            foreach (var p in a.Parameter)
            {
                if (p.Peek() is null) continue;
                // An authored string rides as text — its string face carries
                // the %var%-reference / empty / catalog-description judgements.
                var face = p.Peek() as global::app.type.item.text.@this;
                if (face != null && face.StartsWith("%") && face.EndsWith("%")) continue; // variable reference
                if (p.Type == null) continue;

                // LLM-emitted "" for an unset nullable slot — same shape as
                // validateResponse's normalization, repeated here so detail-pass
                // results (which bypass validateResponse) also get the fix instead
                // of failing in TryConvert below. For non-nullable slots leave the
                // empty string in place so the conversion error surfaces and
                // LlmFixer retries.
                if (face is { } emptyFace && !emptyFace.IsTruthy()
                    && global::app.module.action.build.ValidateResponseHelpers.IsNullableSchemaProp(rows, p.Name))
                {
                    p.SetValue(null);
                    continue;
                }

                // Catalog descriptions ("int = 1", "%var% string", "list<int>?") are schema
                // metadata produced by Modules.Describe(), not values to normalize. They
                // surface when the catalog is fed back through validate (BuilderValidateValid
                // smoke test). Skip — coercing a description string to its declared type fails.
                if (face is { } desc && IsCatalogDescription(desc, p.Type.Name)) continue;

                var targetType = context.App.Type.Get(p.Type.Name);
                if (targetType == null) continue;

                // Scalar PlangType domain types (Path, etc.) carry their wire representation
                // AS the primitive — `Resolve(rawInput, context)` is the runtime constructor.
                // If we eagerly convert here, the saved .pr inflates the primitive into a
                // fully reflected record (Raw, Absolute, FileName, ...) that round-trips
                // poorly. Leave the primitive in the .pr; runtime auto-wraps via the source
                // generator's Resolve convention when the action actually executes.
                if (global::app.type.list.@this.IsScalarPlangType(targetType)) continue;

                // [Choices]-bearing types (Actor, Operator, ...) keep their string form in
                // the .pr — runtime resolves the chosen name via the type's own path
                // (App.GetActor, ctor registry, ...). Eagerly constructing here would
                // either fail (Actor has no usable string ctor) or produce a stateful
                // object that doesn't round-trip cleanly. Same shape as the scalar carve-out.
                if (context.App.Type.Choice.Has(targetType)) continue;

                // Already correctly typed? Skip (e.g. value is bool, target is bool).
                if (targetType.IsInstanceOfType(p.Peek())) continue;

                // Normalize the value to its declared type: p.Type builds itself from the value
                // item (kind from p.Type, declared in loop 1) — string → bool/int, numeric/bool →
                // text. The LLM emitting `Key=404 (int)` for a string-declared Key becomes a text
                // value here. The content door is the throw-on-decline boundary, so a bad value
                // collects into errors for LlmFixer to retry.
                try { p.SetValue(p.Type.Create(p.Peek(), context)); }
                catch (System.InvalidOperationException ex)
                { errors.Add($"{a.Module}.{a.ActionName}.{p.Name}: {ex.Message}"); }
            }

            // Template flag — the ONE %var% detection, done at build. A param whose value
            // carries a %var% is an authored template; stamp type.template="plang" so the
            // .pr carries it and runtime read/render trust it (never re-scan content). Runs
            // LAST so type/kind normalization + conversion can't clobber the flag. The leaf
            // answers its own raw string face (text's chars, a source's raw).
            foreach (var p in a.Parameter)
            {
                var raw = (p.Peek() as global::app.type.item.@this)?.RawText;
                if (raw == null || !global::app.type.item.text.@this.HasVariable(raw)) continue;
                var t = p.Type;
                p.Declare(new app.type.@this(t?.Name ?? "object", t?.Kind?.Name, t?.Strict ?? false, "plang"));
            }
        }
        return errors;
    }

    /// <summary>
    /// Recognizes catalog description strings produced by <see cref="global::app.module.list.@this.Describe"/>:
    /// the four forms <c>"X"</c>, <c>"X?"</c>, <c>"X = default"</c>, <c>"%var% X"</c> (and
    /// combinations). When the catalog itself is fed back through validate, every parameter's
    /// Value is one of these — coercing them through TypeMapping fails because they're
    /// metadata, not data. The match is anchored on <paramref name="typeName"/> (already
    /// stamped from the schema) so an LLM-emitted real value can't accidentally trip it.
    /// </summary>
    // internal-static for unit tests — the helper has 4 distinct match shapes and the
    // production callers only exercise the match-true path through integration tests.
    internal static bool IsCatalogDescription(global::app.type.item.text.@this value, string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        // Span matching is the BCL edge — the text lowers here, inside the
        // method that owns the parse, never at call sites.
        var v = value.Clr<string>()!.AsSpan().Trim();
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
            typeof(global::app.module.IContext),
            typeof(global::app.module.IStep),
            typeof(global::app.module.IChannel),
            typeof(global::app.module.IEvent),
            typeof(global::app.module.IStatic),
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

        var readAction = new file.Read(context)
        {
            Path = context.Ok<path>(prPath)
        };
        var readResult = await app.Run(readAction, context);
        if (!readResult.Success) return errors;

        // File provider auto-deserializes .pr files into a single Goal. A .pr left
        // by an older build can reference a type that has since been renamed or
        // removed — deserialization then throws. That .pr is corrupt from the
        // current schema, so record why and skip the merge: the goal rebuilds from
        // its source rather than crashing the whole build on one stale artefact.
        Goal? prGoal;
        try
        {
            prGoal = (await readResult.Value()) as Goal;
        }
        catch (System.Exception ex) when (ex is not (System.OperationCanceledException
            or System.OutOfMemoryException or System.StackOverflowException))
        {
            errors.Add(new Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to deserialize .pr file at {prPath}: {ex.Message}"
            });
            return errors;
        }

        if (prGoal is null)
        {
            errors.Add(new Info
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}"
            });
            return errors;
        }

        if (prGoal.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase))
            goal.Merge(prGoal);

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
        global::app.goal.step.action.@this action,
        app.@this app, actor.context.@this context)
    {
        if (action.Parameter == null) return;

        foreach (var param in action.Parameter)
        {
            if (!string.Equals(param.Type?.Name, "goal.call", StringComparison.OrdinalIgnoreCase))
                continue;

            var goalCall = ToGoalCall((await param.Value()), context);
            if (goalCall == null || string.IsNullOrEmpty(goalCall.Name))
                continue;

            if (goalCall.Name.Contains('%'))
            {
                param.SetValue(goalCall);
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
            if (resolved.Success && (await resolved.Value()) as Goal is { } g && g.PrPath != null)
            {
                // Pre-resolve the .pr path. A slash-qualified Name keeps its
                // folder prefix in the saved .pr — LoadFromFile leaf-matches it
                // against the loaded goal's own (unqualified) Name at dispatch.
                goalCall.PrPath = g.PrPath;
            }

            param.SetValue(goalCall);
        }
    }

    private static GoalCall? ToGoalCall(object? value, actor.context.@this context)
    {
        if (value is GoalCall gc) return gc;
        // GoalCall builds itself (string / JsonElement / dict → goal.call) through its own entity
        // courier — the same Create door every type uses; a carrier declared goal.call so the
        // family build fires eagerly (the context door would defer a string to a source).
        var carrier = new global::app.data.@this("",
            new global::app.type.item.@null.@this("goal.call"), context: context);
        return context.App.Type["goal.call"]?.Create(value, carrier) as GoalCall;
    }
}
