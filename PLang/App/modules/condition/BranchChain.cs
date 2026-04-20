using PrAction = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace App.modules.condition;

using StepActions = global::App.Goals.Goal.Steps.Step.Actions.@this;

/// <summary>
/// Computes the declared branch-label chain for a condition.if site. Used by both
/// the runtime (so the chain published from If.Run matches what the report shows)
/// and test.discover's scan pass (so untested-but-declared sites can be surfaced).
/// Rules mirror If.Run exactly:
///   - single-action step (only the condition.if): simple path → [true, false]
///   - multi-action step with condition.if at myIndex: orchestrate path →
///     [if, elseif[1], elseif[2], ...] with one entry per condition.if action
///     from myIndex onwards. Trailing condition-less bodies attach to the
///     preceding condition, not as a standalone else (matches current builder output).
/// </summary>
internal static class BranchChain
{
    public static List<string> ComputeFor(StepActions actions, int myIndex)
    {
        if (actions.Count == 1)
            return new List<string> { "true", "false" };

        var chain = new List<string>();
        for (int i = myIndex; i < actions.Count; i++)
        {
            if (IsConditionAction(actions[i]))
                chain.Add(chain.Count == 0 ? "if" : $"elseif[{chain.Count}]");
        }

        // Orchestrate ran on what looked like a single-action — behave like simple.
        if (chain.Count == 0)
            return new List<string> { "true", "false" };

        return chain;
    }

    public static bool IsConditionAction(PrAction action) =>
        string.Equals(action.Module, "condition", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(action.ActionName, "if", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the given condition.if action is the first condition.if in its step.
    /// Used by the coverage subscriber to ignore inner-elseif simple-path firings
    /// that would otherwise mix "true"/"false" labels into an orchestrator's chain.
    /// </summary>
    public static bool IsFirstConditionInStep(PrAction action)
    {
        var step = action.Step;
        if (step == null) return true;
        foreach (var a in step.Actions)
        {
            if (!IsConditionAction(a)) continue;
            return ReferenceEquals(a, action);
        }
        return false;
    }
}
