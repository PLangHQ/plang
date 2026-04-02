using PLang.Runtime2.Engine.FileSystem;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.Engine.Goals.Goal;

/// <summary>
/// Strongly-typed reference to a goal, carrying name, parameters, and optional pre-resolved PrPath.
/// PrPath is nullable because dynamic goal names (containing %variable%) can't resolve at build time.
/// </summary>
public sealed class GoalCall
{
    /// <summary>Goal name to call (e.g., "ProcessData", "/Setup/Init").</summary>
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
    /// Resolves the Goal. Checks step's parent goal sub-goals first, then file.read.
    /// </summary>
    public async Task<@this?> GetGoalAsync(Engine.@this engine, PLangContext context)
    {
        // 1. Check via the action's step's goal chain (action → step → goal → walk up)
        var currentGoal = Action?.Step?.Goal;
        while (currentGoal != null)
        {
            // Check if the goal itself matches (recursive call)
            if (string.Equals(currentGoal.Name, Name, StringComparison.OrdinalIgnoreCase))
                return currentGoal;

            // Check sub-goals
            var subGoal = currentGoal.Goals.FirstOrDefault(g =>
                string.Equals(g.Name, Name, StringComparison.OrdinalIgnoreCase));
            if (subGoal != null) return subGoal;

            // Walk up — find parent goal that contains currentGoal
            // TODO: Goal needs a Parent reference for proper walk-up
            break;
        }

        // 2. Check engine's loaded goals
        var loaded = engine.Goals.Get(Name);
        if (loaded != null) return loaded;

        // 3. Not a sub-goal — file.read the .pr
        var prPath = PrPath;
        if (string.IsNullOrEmpty(prPath) && !string.IsNullOrEmpty(Name))
        {
            var name = Name.TrimStart('/');
            prPath = $".build/{name.ToLowerInvariant()}.pr";
        }

        if (string.IsNullOrEmpty(prPath)) return null;

        // Inject parameters
        if (Parameters != null)
            foreach (var param in Parameters)
                context.MemoryStack.Put(param);

        // file.read the .pr
        var readAction = new modules.file.Read
        {
            Context = context,
            Path = new PathData(prPath, context)
        };
        var result = await engine.RunAction(readAction, context);
        if (!result.Success) return null;

        if (result.Value is not @this goal) return null;

        // Set back-references: steps know their goal
        goal.SetStepBackReferences();
        return goal;
    }
}
