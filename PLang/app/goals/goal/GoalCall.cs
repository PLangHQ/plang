using app.types.path;
using app.variable;
using app.actor.context;
using app.Attributes;

namespace app.goals.goal;

/// <summary>
/// Strongly-typed reference to a goal, carrying name, parameters, and optional pre-resolved PrPath.
/// PrPath is nullable because dynamic goal names (containing %variable%) can't resolve at build time.
/// </summary>
[PlangType("goal.call")]
public sealed class GoalCall : modules.IEvent
{
    /// <summary>Event context — set by Events.Stamp when this GoalCall is an event binding.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public modules.EventContext? Event { get; set; }

    /// <summary>Goal name to call (e.g., "ProcessData", "Setup/Init").</summary>
    [Store, LlmBuilder, Out]
    public string Name { get; init; } = "";

    /// <summary>Whether this tool is safe for concurrent execution. Default false.</summary>
    [Store, LlmBuilder, Out]
    public bool Parallel { get; init; }

    /// <summary>Parameters to pass to the goal, each as a named Data value.</summary>
    [Store, LlmBuilder, Out]
    public List<data.@this>? Parameters { get; set; }
    /// <summary>Pre-resolved .pr file path. Null when the goal name contains %variables%.</summary>
    [Store, Out]
    public global::app.types.path.@this? PrPath { get; set; }

    /// <summary>The action this GoalCall originated from. Set during parameter resolution.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public steps.step.actions.action.@this? Action { get; set; }

    /// <summary>
    /// Resolves the Goal. PrPath is authoritative when set — file.read only.
    /// Otherwise: action chain → app.Goals → file.read fallback.
    /// Returns Data with the Goal as Value, or Data with Error if not found.
    /// </summary>
    public async Task<data.@this> GetGoalAsync(app.@this app, actor.context.@this context)
    {
        // PrPath is authoritative — load from file, no name-based search
        if (PrPath != null)
            return await LoadFromFile(PrPath.ToString(), app, context);

        // 1. Check via the action's step's goal chain (action → step → goal → walk up)
        var currentGoal = Action?.Step?.Goal;
        while (currentGoal != null)
        {
            if (string.Equals(currentGoal.Name, Name, StringComparison.OrdinalIgnoreCase))
                return data.@this.Ok(currentGoal);

            var subGoal = currentGoal.Goals.FirstOrDefault(g =>
                string.Equals(g.Name, Name, StringComparison.OrdinalIgnoreCase));
            if (subGoal != null) return data.@this.Ok(subGoal);

            currentGoal = currentGoal.Parent;
        }

        // 2. Check app's loaded goals
        var loaded = app.Goals.Get(Name);
        if (loaded != null) return data.@this.Ok(loaded);

        // 3. Derive the .pr path from Name and file.read.
        var name = Name.Replace('\\', '/');

        // Caller's folder — the anchor for relative resolution. Compute as a
        // string here because the rest of this method does free-form name math
        // (slash-qualified names, .build prefix, ancestor walks) that's clearer
        // on strings than via Path verbs. Each candidate goes through
        // path.Resolve inside LoadFromFile.
        string? callerDir = Action?.Step?.Goal?.Path?.ToString();
        if (callerDir != null)
        {
            var cut = callerDir.LastIndexOf('/');
            callerDir = cut >= 0 ? callerDir[..cut] : "";
        }

        var slashAt = name.LastIndexOf('/');
        if (slashAt >= 0)
        {
            // Slash-qualified name (BuildGoal/Start): the goal lives in a named
            // folder whose own .build holds the .pr — {folder}/.build/{leaf}.pr,
            // NOT .build/{whole/name}.pr. That folder may be a sibling or an
            // ancestor of the caller's folder, so walk the caller's ancestors
            // before falling back to root- and context-relative.
            var subPath = $"{name[..slashAt]}/.build/{name[(slashAt + 1)..].ToLowerInvariant()}.pr";
            for (var dir = callerDir; !string.IsNullOrEmpty(dir);)
            {
                var hit = await LoadFromFile($"{dir}/{subPath}", app, context);
                if (hit.Success) return hit;
                var up = dir.LastIndexOf('/');
                dir = up > 0 ? dir[..up] : "";
            }
            var slashRoot = await LoadFromFile("/" + subPath, app, context);
            if (slashRoot.Success) return slashRoot;
            return await LoadFromFile(subPath, app, context);
        }

        // Bare name — the .pr sits in the caller's own .build, else root/context.
        var prFile = $".build/{name.ToLowerInvariant()}.pr";

        // Try relative to the calling goal's folder first (e.g., test sub-goals)
        if (callerDir != null)
        {
            var goalResult = await LoadFromFile($"{callerDir}/{prFile}", app, context);
            if (goalResult.Success) return goalResult;
        }

        // Try root-relative (for user goals calling other goals in same project)
        var rootResult = await LoadFromFile("/" + prFile, app, context);
        if (rootResult.Success) return rootResult;

        // Try context-relative
        return await LoadFromFile(prFile, app, context);
    }

    private async Task<data.@this> LoadFromFile(string prPath, app.@this app, actor.context.@this context)
    {
        var readAction = new modules.file.Read
        {
            Context = context,
            Path = data.@this<path>.Ok(path.Resolve(prPath, context))
        };
        var result = await app.RunAction(readAction, context);
        if (!result.Success) return result;

        if (result.Value is not @this goal)
            return data.@this.FromError(new global::app.error.ActionError(
                $"File '{prPath}' did not deserialize to a Goal", "InvalidPrFile", 400));

        // Wire back-references: Goal.App, Step.Goal for root and sub-goals
        goal.App = app;
        foreach (var step in goal.Steps)
            step.Goal = goal;
        foreach (var subGoal in goal.Goals)
        {
            subGoal.App = app;
            subGoal.Parent = goal;
            foreach (var step in subGoal.Steps)
                step.Goal = subGoal;
        }

        // Stash where the .pr was loaded from — Goal.GetRuntimeDirectory uses this
        // so file.read with a relative path resolves against the goal's actual
        // on-disk directory (works in child Apps where Path was set under a
        // different root and would otherwise mis-resolve).
        var prPathResolved = global::app.types.path.@this.Resolve(prPath, context);
        goal.LoadedFromPrPath = prPathResolved;
        foreach (var subGoal in goal.Goals)
            subGoal.LoadedFromPrPath = prPathResolved;

        // Match by name — the loaded goal or one of its sub-goals. A slash-
        // qualified Name (BuildGoal/Start) carries a folder prefix that the
        // loaded goal's own Name never has, so match on the leaf segment.
        var matchName = Name;
        var nameSlash = matchName.LastIndexOfAny(new[] { '/', '\\' });
        if (nameSlash >= 0) matchName = matchName[(nameSlash + 1)..];

        @this? found;
        if (string.IsNullOrEmpty(matchName) || string.Equals(goal.Name, matchName, StringComparison.OrdinalIgnoreCase))
            found = goal;
        else
            found = goal.Goals.FirstOrDefault(g => string.Equals(g.Name, matchName, StringComparison.OrdinalIgnoreCase));

        if (found == null)
            return data.@this.FromError(new global::app.error.ActionError(
                $"Goal '{Name}' not found in '{prPath}'", "GoalNotFound", 404));

        return data.@this.Ok(found);
    }
}
