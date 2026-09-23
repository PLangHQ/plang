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
            return context.Ok(new global::app.type.item.list.@this<Goal>(context));

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
                return context.Ok(new global::app.type.item.list.@this<Goal>(context));
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

        var result = context.Ok(new global::app.type.item.list.@this<Goal>(allGoals, context));
        if (allErrors.Count > 0)
            result.Warnings = allErrors;
        return result;
    }

    // --- Fold: indent-authored sub-steps → gate-action Child ---

    public async Task<data.@this> Fold(fold action)
    {
        var context = action.Context;
        var goal = (await action.Goal.Value())!;
        var errors = new List<global::app.error.IError>();
        Fold(goal, errors);
        if (errors.Count == 0) return context.Ok(true);

        // Surface every A4 violation: the first is the root, the rest are its causes —
        // each error carries its own offending step (location), not a flattened string.
        var root = errors[0];
        for (int e = 1; e < errors.Count; e++) root.list.Add(errors[e]);
        return context.Error(root);
    }

    // Folds a goal's own steps, then recurses its sub-goals. Sets each goal's Step to the
    // nested projection — the goal owns its (now-tree) step collection.
    private void Fold(Goal goal, List<global::app.error.IError> errors)
    {
        goal.Step = Fold(goal.Step, errors);
        foreach (var subGoal in goal.Child) Fold(subGoal, errors);
    }

    // Flat + Indent → tree: a step's deeper-indented followers move into that step's gate
    // action (the IsCondition action) Child; recursion composes nested blocks. A block under
    // a non-condition step is an authoring error (A4) — recorded against the offending step,
    // never silently dropped or kept flat. Real steps only; nothing is synthesized here.
    // The fold holds the step/action nodes it is assembling (its own typed positional face), never
    // a harvested element list.
    private global::app.goal.step.list.@this Fold(
        global::app.goal.step.list.@this flat, List<global::app.error.IError> errors)
    {
        var top = new global::app.goal.step.list.@this();   // Add each real step into the node
        int i = 0;
        while (i < flat.Count)
        {
            var step = flat[i];
            int j = i + 1;
            while (j < flat.Count && flat[j].Indent > step.Indent) j++;   // gather the deeper block

            if (j > i + 1)
            {
                var block = new global::app.goal.step.list.@this();
                for (int k = i + 1; k < j; k++) block.Add(flat[k]);

                global::app.goal.step.action.@this? gate = null;
                for (int k = 0; k < step.Action.Count && gate == null; k++)
                    if (step.Action[k].IsCondition) gate = step.Action[k];
                if (gate == null)
                    errors.Add(new global::app.error.StepError(
                        $"indented steps under non-condition step '{step.Text}'",
                        step, "IndentUnderNonCondition", 400));
                else
                    gate.Child = Fold(block, errors);
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
        var goal = (await action.Goal.Value())!;

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

        // Final safety net before persisting: the goal judges itself. Refusing to write the .pr is
        // preferable to saving a half-built artifact the runtime can't execute.
        if (await goal.Validate(context) is { } invalid) return context.Error(invalid);

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

    // --- Validate ---

    public async Task<data.@this> Validate(validate action)
    {

        var app = action.Context.App;
        var context = action.Context;
        var modules = app.Module;

        var actionList = action.Actions == null ? null : await action.Actions.Value() as global::app.type.item.list.@this;

        // Each row opens through its own action door — params intact, no CLR peel. The chain is the
        // node the graph judges itself through; `actions` is the same instances, for the
        // construction passes that still take a plain list.
        var actions = new List<global::app.goal.step.action.@this>();
        var chain = new global::app.goal.step.action.list.@this();
        foreach (var row in actionList?.Items ?? (IReadOnlyList<data.@this>)System.Array.Empty<data.@this>())
            if (await row.Value<global::app.goal.step.action.@this>() is { } ae)
            {
                actions.Add(ae);
                chain.Add(ae);
            }

        await ResolveGoalCallPaths(actions, app, context);
        var normalizationErrors = NormalizeParameterTypes(actions, modules, context);

        foreach (var a in actions)
        {
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (a.Parameter != null)
                foreach (var p in a.Parameter) paramNames.Add(p.Name);
            a.Default = modules.GetDefaults(a.Module.Name, a.Name, paramNames) is { } defs
                ? new global::app.goal.step.action.parameter.list.@this(defs) : null;

            // goal.call REPAIR — construction, not judgement. A name that survives this and still
            // carries a dot is the action's own verdict to give (action.Validate).
            if (a.Parameter != null)
            {
                foreach (var p in a.Parameter)
                {
                    // Only a goal.call SLOT is opened. Asking every parameter for its value would
                    // resolve things the build must not read — an authored `%count%` is unset at
                    // build time, and reading it turns a repair pass into a resolution failure.
                    if (!string.Equals(p.Type?.Name, "goal.call", StringComparison.OrdinalIgnoreCase)) continue;
                    // A catalog row's "goal.call" description is TEXT, not a goal.call, so it does
                    // not match and needs no guard — the structure is the guard.
                    if (await p.Value() is not GoalCall goalCall) continue;
                    if (string.IsNullOrEmpty(goalCall.Name)) continue;
                    if (goalCall.Name.Contains('%')) continue;  // %var% resolves at runtime
                    if (goalCall.Name.Contains('.'))
                    {
                        // Repair the recurring LLM leak of stuffing call notation into the
                        // goal NAME itself — e.g. event.on's GoalToCall coming back as
                        // "goal.call(LogBefore)" / "goal.call LogBefore". The real name is
                        // the inner identifier. Repair + warn rather than reject: rejecting
                        // triggers a FixValidation retry that tends to DEGRADE (a bare
                        // `goal` param, dropping the required Trigger → "trigger must have
                        // a value" at runtime). Mirrors the module-name-separator repair above.
                        var m = System.Text.RegularExpressions.Regex.Match(
                            goalCall.Name, @"^goal\.call\s*\(?\s*([A-Za-z_][\w/]*)\s*\)?$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            a.Warning.Add(new global::app.warning.@this {
                                Key = "GoalCallNameRepaired",
                                Message = $"goal.call.Name '{goalCall.Name}' carried the formal goal.call notation; repaired to '{m.Groups[1].Value}'."
                            });
                            // Name is init-only; rebuild with the repaired name, carry the rest.
                            p.SetValue(new GoalCall {
                                Name = m.Groups[1].Value,
                                Parallel = goalCall.Parallel,
                                Parameter = goalCall.Parameter,
                                PrPath = goalCall.PrPath,
                            });
                        }
                    }
                }
            }

        }

        // Construction is done; the graph judges itself and the builder only reacts. Normalization
        // failures are the builder's own — it did the converting — so they ride as causes beside
        // the chain's.
        var verdict = await chain.Validate(context);
        if (verdict != null || normalizationErrors.Count > 0)
        {
            var causes = normalizationErrors
                .Select(e => (global::app.error.IError)new global::app.error.Error(e, "NormalizeParameter", 400))
                .ToList();
            if (verdict != null) causes.AddRange(verdict.list.Count > 0 ? verdict.list : new() { verdict });

            return context.Error(new global::app.error.Error(
                string.Join("; ", causes.Select(c => c.Message)), "BuildValidation", 400) { list = causes });
        }

        // Per-action Build() pass — each handler may stamp a type on the step's
        // terminal variable.set. See IClass.Build for the contract.
        var buildErrors = await RunBuildPass(actions, context);
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
    internal static async Task<List<string>> RunBuildPass(Actions actions,
        actor.context.@this context)
    {
        var errors = new List<string>();
        foreach (var a in actions)
        {
            var (instance, _) = a.Instance(context);
            if (instance == null) continue;
            // Resolve builds a populated instance (params decoded); Build() reads them.
            var (handler, resolveErr) = await instance.Resolve(a, context);
            if (resolveErr != null)
            {
                errors.Add($"{a.Module}.{a.Name}: {resolveErr.Message}");
                break;
            }
            if (handler is not global::app.module.IClass classified) continue;
            var buildResult = await classified.Build();
            if (!buildResult.Success)
            {
                errors.Add($"{a.Module}.{a.Name}: {buildResult.Error?.Message ?? "Build() failed"}");
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

    /// <summary>
    /// Normalizes parameter values to match their declared type.
    /// LLMs are non-deterministic — they may produce "false" (string) instead of false (bool).
    /// This runs at build time so the .pr file has correct types.
    /// Returns conversion errors so the caller can carry them as causes beside the graph's own
    /// verdicts — without this, an LLM-emitted value that can't convert to the declared type
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
            var actionType = modules.GetActionType(a.Module.Name, a.Name);
            // The catalog element's declared rows — the ONE reflection site, read for nullable-slot
            // detection below instead of re-reflecting with a NullabilityInfoContext.
            var rows = a.Module[a.Name]?.Property.Rows;
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
                        p.Declare(app.type.@this.Create(typeName, context: context), context);

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
                            p.Declare(built.Type, context);
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

                // LLM-emitted "" for an unset nullable slot is an unset slot, not a value to
                // convert. For a non-nullable slot the empty string stays, so the conversion
                // error surfaces and the build retries.
                if (face is { } emptyFace && !emptyFace.IsTruthy()
                    && rows?.FirstOrDefault(r => string.Equals(r.Name, p.Name, StringComparison.OrdinalIgnoreCase))?.Nullable == true)
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
                { errors.Add($"{a.Module}.{a.Name}.{p.Name}: {ex.Message}"); }
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
                p.Declare(app.type.@this.Create(t?.Name ?? "object", t?.Kind?.Name, t?.Strict ?? false, context, "plang"), context);
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
            if (action.Modifier != null)
            {
                foreach (var mod in action.Modifier)
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
