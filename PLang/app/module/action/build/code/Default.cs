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

        var step = (await action.Step.Value())!;

        for (int i = 0; i < step.Action.Count; i++)
        {
            var a = step.Action[i];
            var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in a.Parameter) paramNames.Add(p.Name);
            a.Default = modules.GetDefaults(a.Module.Name, a.Name, paramNames) is { } defs
                ? new global::app.goal.step.action.parameter.list.@this(defs) : null;
        }

        // Construction is done; the step judges itself and the builder only reacts. The verdict stays
        // whole — it names the step, which the re-prompt needs.
        if (await step.Validate(context) is { } verdict)
            return context.Error(verdict);

        // The chain finishes itself — each action binds its handler, runs its Validate()/Build()
        // hooks and walks what it holds (modifiers, recovery, branch body). The builder reacts.
        if (await step.Action.Build(context) is { } failed) return context.Error(failed);

        return context.Ok(true);
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

}
