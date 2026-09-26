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
            return context.Ok(new global::app.type.item.list.@this<Goal>());

        // Filter by app.Build.Files if set (--build={"files":[...]})
        // Honor the user's specified order — building has bootstrapping concerns
        // (e.g., system/builder rebuilding itself: BuildGoal must come LAST so
        // earlier iterations use the previous in-memory build pipeline).
        // Each row lifts to a path at ITS door — a JSON-string row becomes a path (path.Create), a
        // %var% row resolves the variable. This is the materialize-on-read the plang-typed Build.Files
        // buys: the walk stored the list lazily, the consumer opens each row here.
        var filters = new List<path>();
        foreach (var row in app.Build.Files.Items(context))
            if (await row.Value<global::app.type.item.path.@this>() is { } bf)
                filters.Add(bf);

        if (filters.Count > 0)
        {
            // The affix/filename filter semantics live on path (path.Matches) —
            // the type owns its containment math, relative to the builder's root.
            bool MatchesPattern(path f, path bf) => f.Matches(bf, context).Value;

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
                return context.Ok(new global::app.type.item.list.@this<Goal>());
        }

        var allGoals = new List<Goal>();
        // A source the build cannot read is a verdict it cannot proceed with — every unreadable file
        // is collected (one run shows them all), then the build fails once, each read error whole.
        var unreadable = new List<(path File, global::app.error.Error Error)>();

        foreach (var file in files)
        {
            var readAction = new file.Read(context) { Path = context.Ok<path>(file) };
            var readResult = await app.Run(readAction, context);
            if (!readResult.Success)
            {
                unreadable.Add((file, readResult.Error ?? new global::app.error.Error($"Failed to read {file.Raw}", "FileReadError", 400)));
                continue;
            }

            var text = (await readResult.Value())?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var goal = Goal.Parse(text, file, context);
            if (goal == null) continue;

            await MergePrData(goal, app, context);
            allGoals.Add(goal);
        }

        if (unreadable.Count > 0)
            return context.Error(new global::app.error.Error(
                $"Could not read {unreadable.Count} goal file(s): {string.Join(", ", unreadable.Select(u => u.File.Raw))}",
                "FileReadError", 400)
            {
                list = unreadable.Select(u => u.Error).ToList(),
            });

        _buildTimer.Restart();

        return context.Ok(new global::app.type.item.list.@this<Goal>(allGoals));
    }

    // --- Fold: indent-authored sub-steps → gate-action Child ---

    public async Task<data.@this> Fold(fold action)
    {
        var context = action.Context;
        var goal = (await action.Goal.Value())!;
        var errors = new List<global::app.error.Error>();
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
    private void Fold(Goal goal, List<global::app.error.Error> errors)
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
        global::app.goal.step.list.@this flat, List<global::app.error.Error> errors)
    {
        var top = new global::app.goal.step.list.@this();   // Add each real step into the node
        int i = 0;
        while (i < flat.Count)
        {
            var step = flat[i];
            var block = flat.Body(i);   // the steps indented under it — the list answers
            int j = i + 1 + block.CountRaw;

            if (block.CountRaw > 0)
            {
                global::app.goal.step.action.@this? gate = null;
                for (int k = 0; k < step.Code.Count && gate == null; k++)
                    if (step.Code[k].IsCondition) gate = step.Code[k];
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

        // The goal writes its OWN .pr through Output (the value-owns-serialization path), as the text
        // of a file: Store view, characters as themselves, a trailing new line. Symmetric with the goal
        // reader's bare read.
        var serializer = (global::app.channel.serializer.plang.@this)
            context.Actor.Channel.Serializers.GetOrDefault("application/plang");
        var json = await serializer.Text(goal);

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

        var context = action.Context;

        var step = (await action.Step.Value())!;

        // Freeze the class's [Default] for every property the step did not set — a built app runs
        // the same on a later runtime that changes a default. The defaults are the catalog twin's.
        for (int i = 0; i < step.Code.Count; i++)
        {
            var a = step.Code[i];
            if (a.Module[a.Name] is not { } catalog) continue;
            foreach (var declared in catalog.Property)
            {
                if (declared.Default == null || a[declared.Name] != null || a.Default[declared.Name] != null) continue;
                var frozen = new data.@this(declared.Name.ToLowerInvariant(), declared.Default, context: context);
                a.Default.Add(new global::app.type.property.@this
                    { Name = frozen.Name, Type = frozen.Type, Value = frozen.Peek() });
            }
        }

        // Construction is done; the step judges itself and the builder only reacts. The verdict stays
        // whole — it names the step, which the re-prompt needs.
        if (await step.Validate(context) is { } verdict)
            return context.Error(verdict);

        // The chain finishes itself — each action binds its handler, runs its Validate()/Build()
        // hooks and walks what it holds (modifiers, recovery, branch body). The builder reacts.
        if (await step.Code.Build(context) is { } failed) return context.Error(failed);

        return context.Ok(true);
    }

    // --- Match ---

    public async Task<data.@this> Match(match action)
    {
        var context = action.Context;
        var goal = (await action.Goal.Value())!;
        var answer = (await action.Answer.Value())!;
        var entries = answer.Get("step", context) is { } held
            ? await held.Value<global::app.type.item.list.@this>() : null;

        // The goal's steps judge the answer; the builder only reacts.
        if (await goal.Step.Match(entries ?? new global::app.type.item.list.@this(), context) is { } refusal)
            return context.Error(refusal);
        return context.Ok(true);
    }

    // --- Pick ---

    public async Task<data.@this> Pick(pick action)
    {
        var context = action.Context;
        var goal = (await action.Goal.Value())!;
        var answer = (await action.Answer.Value())!;
        // Each step takes the answers under its own ids; the builder only hands the answer over.
        foreach (var step in goal.Step.Items()) await step.Pick.Take(answer, context);
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
            $"builder.merge: step.Index={step?.Index} step.Code={step?.Code.Count} " +
            $"from.Index={from?.Index} from.Keep={from?.Keep} from.Code={from?.Code.Count}");

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
    /// Merges existing .pr data into a goal. A corrupt .pr is a build diagnostic about the goal —
    /// it rebuilds from its source, and the warning hangs on the goal.
    /// </summary>
    private static async Task MergePrData(Goal goal, app.@this app,
        actor.context.@this context)
    {
        var prPath = goal.PrPath;
        if (prPath == null) return;

        var readAction = new file.Read(context)
        {
            Path = context.Ok<path>(prPath)
        };
        var readResult = await app.Run(readAction, context);
        if (!readResult.Success) return;

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
            goal.Warning.Add(new global::app.warning.@this
            {
                Key = "CorruptPrFile",
                Message = $"Failed to deserialize .pr file at {prPath}: {ex.Message}"
            });
            return;
        }

        if (prGoal is null)
        {
            goal.Warning.Add(new global::app.warning.@this
            {
                Key = "CorruptPrFile",
                Message = $"Failed to parse .pr file at {prPath}"
            });
            return;
        }

        if (prGoal.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase))
            goal.Merge(prGoal);
    }

}
