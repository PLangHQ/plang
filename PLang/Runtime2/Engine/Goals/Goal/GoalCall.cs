using PLang.Runtime2.Engine.FileSystem;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.Engine.Goals.Goal;

/// <summary>
/// Strongly-typed reference to a goal, carrying name, parameters, and optional pre-resolved PrPath.
/// PrPath is nullable because dynamic goal names (containing %variable%) can't resolve at build time.
/// </summary>
public sealed class GoalCall : modules.IEvent
{
    /// <summary>Event context — set by Events.Stamp when this GoalCall is an event binding.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public modules.EventContext? Event { get; set; }

    /// <summary>Goal name to call (e.g., "ProcessData", "Setup/Init").</summary>
    [Store, LlmBuilder]
    public string Name { get; init; } = "";
    /// <summary>Description of what this goal does — used when GoalCall is a tool definition for an LLM.</summary>
    [Store, LlmBuilder]
    public string? Description { get; init; }

    /// <summary>Whether this tool is safe for concurrent execution. Default false.</summary>
    [Store, LlmBuilder]
    public bool Parallel { get; init; }

    /// <summary>Parameters to pass to the goal, each as a named Data value.</summary>
    [Store, LlmBuilder]
    public List<Data>? Parameters { get; init; }
    /// <summary>Pre-resolved .pr file path. Null when the goal name contains %variables%.</summary>
    [Store]
    public string? PrPath { get; set; }

    /// <summary>The action this GoalCall originated from. Set during parameter resolution.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Steps.Step.Actions.Action.@this? Action { get; set; }

    /// <summary>
    /// Resolves the Goal. PrPath is authoritative when set — file.read only.
    /// Otherwise: action chain → engine.Goals → file.read fallback.
    /// </summary>
    public async Task<@this?> GetGoalAsync(Engine.@this engine, PLangContext context)
    {
        // PrPath is authoritative — load from file, no name-based search
        if (!string.IsNullOrEmpty(PrPath))
            return await LoadFromFile(PrPath, engine, context);

        // 1. Check via the action's step's goal chain (action → step → goal → walk up)
        var currentGoal = Action?.Step?.Goal;
        while (currentGoal != null)
        {
            if (string.Equals(currentGoal.Name, Name, StringComparison.OrdinalIgnoreCase))
                return currentGoal;

            var subGoal = currentGoal.Goals.FirstOrDefault(g =>
                string.Equals(g.Name, Name, StringComparison.OrdinalIgnoreCase));
            if (subGoal != null) return subGoal;

            currentGoal = currentGoal.Parent;
        }

        // 2. Check engine's loaded goals
        var loaded = engine.Goals.Get(Name);
        if (loaded != null) return loaded;

        // 3. Derive PrPath from Name and file.read
        var prFile = $".build/{Name.ToLowerInvariant()}.pr";

        // Try relative to the calling goal's folder first (e.g., test sub-goals)
        var callingGoal = Action?.Step?.Goal;
        if (callingGoal?.Path != null)
        {
            var goalDir = callingGoal.Path;
            var lastSlash = goalDir.LastIndexOf('/');
            if (lastSlash >= 0)
                goalDir = goalDir[..lastSlash];
            var goalRelative = $"{goalDir}/{prFile}";
            var goalResult = await LoadFromFile(goalRelative, engine, context);
            if (goalResult != null) return goalResult;
        }

        // Try root-relative (for user goals calling other goals in same project)
        var rootResult = await LoadFromFile("/" + prFile, engine, context);
        if (rootResult != null) return rootResult;

        // Try context-relative
        return await LoadFromFile(prFile, engine, context);
    }

    private async Task<@this?> LoadFromFile(string prPath, Engine.@this engine, PLangContext context)
    {
        // Inject parameters
        if (Parameters != null)
            foreach (var param in Parameters)
                context.MemoryStack.Put(param);

        var readAction = new modules.file.Read
        {
            Context = context,
            Path = new PLangPath(prPath, context)
        };
        var result = await engine.RunAction(readAction, context);
        if (!result.Success) return null;

        if (result.Value is not @this goal) return null;

        // Match by name — the loaded goal or one of its sub-goals
        @this? found;
        if (string.IsNullOrEmpty(Name) || string.Equals(goal.Name, Name, StringComparison.OrdinalIgnoreCase))
            found = goal;
        else
            found = goal.Goals.FirstOrDefault(g => string.Equals(g.Name, Name, StringComparison.OrdinalIgnoreCase));

        if (found == null) return null;

        found.SetStepBackReferences();
        return found;
    }
}
