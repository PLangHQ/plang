using System.Diagnostics;
using app.Utils;
using System.Text.Json;
using app.goal;
using Goal = app.goal.@this;
using Actions = System.Collections.Generic.List<app.goal.step.action.@this>;

namespace app.module.build.code;

public class Default : IBuilder
{
    public string Name => "default";
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

        var listResult = await path.Resolve(rootRelative, context).List("*.goal", true, context);
        if (!listResult.Success)
            return listResult;

        var files = (await listResult.Value()).Clr<List<path>>();
        if (files == null || files.Count == 0)
            return context.Ok(new global::app.type.item.list.@this<Goal>());

        // Filter by build's files setting if set (--build={"files":[...]}, %!build.setting.files%)
        // Honor the user's specified order — building has bootstrapping concerns
        // (e.g., system/builder rebuilding itself: BuildGoal must come LAST so
        // earlier iterations use the previous in-memory build pipeline).
        // Each row lifts to a path at ITS door — a JSON-string row becomes a path (path.Create), a
        // %var% row resolves the variable. This is the materialize-on-read the plang-typed Build.Files
        // buys: the walk stored the list lazily, the consumer opens each row here.
        // A files entry names a goal file of the app being built: a relative one is from the app's root
        // (as the user types it at the root), never from the builder's own folder.
        var filters = new List<path>();
        foreach (var row in context.Setting.Of<global::app.module.build.setting.@this>().Files.Items(context))
            if ((await row.Value())?.ToString() is { Length: > 0 } entry)
                filters.Add(path.Resolve(entry.StartsWith('/') || entry.StartsWith('\\') ? entry : "/" + entry, context));

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
                return context.Error(new global::app.error.Error(
                    $"no goal matched {string.Join(", ", filters.Select(f => $"'{f.Raw}'"))} under {rootRelative}",
                    "NoGoalMatched", 404));
        }

        var allGoals = new List<Goal>();
        // A source the build cannot read is a verdict it cannot proceed with — every unreadable file
        // is collected (one run shows them all), then the build fails once, each read error whole.
        var unreadable = new List<(path File, global::app.error.Error Error)>();

        foreach (var file in files)
        {
            var readResult = await file.Read(context);
            if (!readResult.Success)
            {
                unreadable.Add((file, readResult.Error ?? new global::app.error.Error($"Failed to read {file.Raw}", "FileReadError", 400)));
                continue;
            }

            var text = (await readResult.Value())?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var goal = Goal.Parse(text, file, context);
            if (goal == null) continue;

            await MergePrData(goal, context);
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
        foreach (var subGoal in goal.Child.Items()) Fold(subGoal, errors);
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

        var prPath = goal.PrPath;
        if (prPath == null)
            return context.Error(new global::app.error.ActionError("Goal has no Path set, cannot derive PrPath", "NoPrPath", 400));

        // Final safety net before persisting: the goal judges itself. Refusing to write the .pr is
        // preferable to saving a half-built artifact the runtime can't execute.
        if (await goal.Validate(context) is { } invalid) return context.Error(invalid);

        // The goal is saved to its .pr; the file's format (goal's own) writes it — symmetric with the goal
        // reader's bare read.
        var saveResult = await prPath.Save(context.Ok(goal), context);

        var elapsed = _buildTimer.Elapsed;
        await context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(
            $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s)");
        _buildTimer.Restart();

        return saveResult.Success ? context.Ok(true) : saveResult;
    }

    // --- Match ---

    public async Task<data.@this> Match(match action)
    {
        var context = action.Context;
        var goal = (await action.Goal.Value())!;
        var answer = (await action.Answer.Value())!;
        var confirmed = action.Confirmed == null ? null : await action.Confirmed.Value();

        // The goal's steps read and judge the answer; the builder only reacts.
        if (await goal.Step.Read(answer.ToString(), context, confirmed is { IsNull: false } ? confirmed : null) is { } refusal)
            return context.Error(refusal);

        // A result-only action (one with an [Input] property — it reads its input and answers a new value of
        // it, changing nothing, e.g. list.query's List) whose result goes nowhere is a silent no-op: a query
        // never changes its input, so `sort %people% by age` with no destination must write the answer back to
        // %people%. Inserted here, AFTER Read, so Cover's "is it listed?" check (which refuses the writer's own
        // variable.set) doesn't apply. A literal input has no name to write back to — refused, so FixSteps retries.
        if (await WriteBack(goal, context) is { } wbRefusal)
            return context.Error(wbRefusal);

        return context.Ok(true);
    }

    // Give the deterministic write-back to a destination-less result-only action. The write-back target is
    // the action's [Input] property's variable (its List, %people%), read from the catalog element — a built
    // action's own properties don't carry IsInput (the formal reader builds Name/Type/Value only).
    private async Task<global::app.error.Error?> WriteBack(
        global::app.goal.@this goal, global::app.actor.context.@this context)
    {
        var modules = context.App.module.list;
        var steps = goal.Step.Items().ToList();
        for (var s = 0; s < steps.Count; s++)
        {
            var step = steps[s];
            var code = step.Code.Items().ToList();
            for (var a = 0; a < code.Count; a++)
            {
                var act = code[a];
                var inputDef = act.Module?[act.Name]?.Input;      // the [Input] property, from the catalog element
                if (inputDef is null) continue;
                // the result is already taken? only a later action IN THIS STEP reads the register — %!data% is
                // transient, clobbered the moment the next step runs its own action, so the next step reading it
                // reads its own result, never this one's. Same-step only, and it reads it whole, by member or by
                // index (all share the %!data% root), so ask the parsed variables, not the text.
                if (code.Skip(a + 1).Any(ReadsData)) continue;
                // the write-back goes to the input's own variable (list.query's List, %people%). The value holds
                // its variables parsed, so ask them, not a regex of its text: it must be exactly ONE, whole (the
                // value is nothing but it) and not a %!system% root — a literal, several, or %!data% has no name
                // of the step's own to write back to, refused so FixSteps retries with a destination.
                var input = act[inputDef.Name]?.Value;
                var target = input is { HasVariable: true } && input.Variable.Count == 1 ? input.Variable[0] : null;
                if (target is null || target.Code.Root.Name.StartsWith('!')
                    || !string.Equals(target.Text, input!.ToString()?.Trim(), System.StringComparison.Ordinal))
                    return new global::app.error.ValidationError(
                        $"step {step.Index}: {act.Module!.Name}.{act.Name} answers a new value and changes nothing — give it a destination (`…, write to %result%`); a literal or %!system% input has no name to write the answer back to.",
                        "NoWriteBackTarget");
                var read = new global::app.goal.step.action.formal.Reader(step, modules)
                    .Read($"variable.set(Name={target.Text}, Value=%!data%)", context);
                if (!read.Success) return read.Error;
                // right after the action, not at the step's end: a later output/write in the step would otherwise
                // run first and leave its own result in %!data% before the set reads it.
                if (await read.Value() is global::app.goal.step.action.list.@this wb)
                    step.Code.Insert(step.Code.IndexOf(act) + 1, wb);
            }
        }
        return null;

        static bool ReadsData(global::app.goal.step.action.@this a)
            => a.Property.Any(p => p.Value is { } v && v.Variable.Any(
                x => string.Equals(x.Code.Root.Name, "!data", System.StringComparison.OrdinalIgnoreCase)));
    }

    // --- Pick ---

    public async Task<data.@this> Pick(pick action)
    {
        var context = action.Context;
        var goal = (await action.Goal.Value())!;
        var answer = (await action.Answer.Value())!;
        var popular = new List<string>();
        foreach (var name in (await action.Popular.Value())!.Items(context)) popular.Add((await name.Value())!.ToString());
        // Each step takes the answers under its own ids; the builder only hands the answer over.
        foreach (var step in goal.Step.Items()) await step.Pick.Take(answer, popular, context);
        // What the picks know, walked: each step is left the types of the variables it reads.
        await goal.Step.Scope(context);
        return context.Ok(true);
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
    private static async Task MergePrData(Goal goal, actor.context.@this context)
    {
        var prPath = goal.PrPath;
        if (prPath == null) return;

        var readResult = await prPath.Read(context);
        if (!readResult.Success) return;

        // File provider auto-deserializes .pr files into a single Goal. A .pr left
        // by an older build can reference a type that has since been renamed or
        // removed — deserialization then throws. That .pr is corrupt from the
        // current schema, so record why and skip the merge: the goal rebuilds from
        // its source rather than crashing the whole build on one stale artefact.
        // Only the reader's refusal (an outdated format names itself) or the .pr's own
        // malformed JSON is a corrupt .pr; any other exception is a bug and bubbles.
        Goal? prGoal;
        try
        {
            prGoal = (await readResult.Value()) as Goal;
        }
        catch (System.Exception ex) when (ex is global::app.error.AppException or System.Text.Json.JsonException)
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

        if (!prGoal.Name.Equals(goal.Name, StringComparison.OrdinalIgnoreCase)) return;
        goal.Merge(prGoal);
        await goal.Reopen(context);
    }

}
