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
        // PrPath has the path, Name is just the goal name
        var prPath = PrPath;
        if (string.IsNullOrEmpty(prPath) && !string.IsNullOrEmpty(Name))
            prPath = $".build/{Name.ToLowerInvariant()}.pr";

        if (string.IsNullOrEmpty(prPath)) return null;

        // Inject parameters
        if (Parameters != null)
            foreach (var param in Parameters)
                context.MemoryStack.Put(param);

        // file.read the .pr — resolve relative paths against the user's goal folder,
        // not the system goal (context.Goal may be system/run.pr)
        var resolveContext = context;
        if (!prPath.StartsWith('/') && !prPath.StartsWith('\\'))
        {
            // Find the user goal folder: action chain → or MemoryStack %step%
            // Resolve against engine root (user's app directory)
            var rootPath = "/" + prPath;
            var readAction2 = new modules.file.Read
            {
                Context = context,
                Path = new PathData(rootPath, context)
            };
            var result2 = await engine.RunAction(readAction2, context);
            if (result2.Success && result2.Value is @this loadedGoal2)
            {
                @this? found2;
                if (string.IsNullOrEmpty(Name) || string.Equals(loadedGoal2.Name, Name, StringComparison.OrdinalIgnoreCase))
                    found2 = loadedGoal2;
                else
                    found2 = loadedGoal2.Goals.FirstOrDefault(g => string.Equals(g.Name, Name, StringComparison.OrdinalIgnoreCase));
                if (found2 != null) { found2.SetStepBackReferences(); return found2; }
            }
        }

        var readAction = new modules.file.Read
        {
            Context = context,
            Path = new PathData(prPath, context)
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
