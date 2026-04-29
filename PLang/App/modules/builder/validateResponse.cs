using Goal = App.Goals.Goal.@this;

namespace App.modules.builder;

/// <summary>
/// Validates the structural integrity of an LLM build response.
/// Collects ALL errors (step count, indexes, missing actions, unknown modules)
/// and returns them in a structured message so LlmFixer can show them to the LLM.
///
/// Operates on the typed <see cref="BuildResponse"/> so there are no JsonElement /
/// IDictionary forks — the framework's Data&lt;BuildResponse&gt; pipeline deserializes
/// the LLM's JsonElement once with Json.CaseInsensitiveRead.
/// </summary>
[System.ComponentModel.Description("Validate an LLM build response for structural integrity — step count, indexes, empty actions, keep:true with no prior")]
[Action("validateResponse")]
public partial class validateResponse : IContext
{
    /// <summary>The LLM response. Framework deserializes the raw JsonElement to BuildResponse.</summary>
    [IsNotNull]
    public partial Data.@this<BuildResponse> StepResults { get; init; }

    /// <summary>The goal being built — used to verify step count and prior actions.</summary>
    [IsNotNull]
    public partial Data.@this<Goal> Goal { get; init; }

    public Task<App.Data.@this> Run()
    {
        var response = StepResults.Value;
        var goal = Goal.Value;

        // Identify which parameter is null and dump enough state for LlmFixer +
        // logs to see *why*. "StepResults or Goal is null" was actively misleading —
        // the two failures have completely different causes (LLM response shape vs.
        // %goal% propagation), and a single message hides that.
        if (response == null || goal == null)
        {
            var problems = new List<string>();
            if (response == null)
            {
                var sr = StepResults;
                problems.Add(
                    sr == null
                        ? "StepResults parameter not bound (Data is null)"
                        : sr.IsInitialized
                            ? $"StepResults.Value is null but Data was initialized — deserialization to BuildResponse returned null. Raw value type: {sr.RawValue?.GetType().Name ?? "null"}"
                            : "StepResults parameter is uninitialized (LLM call wrote null/missing %stepResults%)");
            }
            if (goal == null)
            {
                var g = Goal;
                problems.Add(
                    g == null
                        ? "Goal parameter not bound (Data is null)"
                        : g.IsInitialized
                            ? $"Goal.Value is null but Data was initialized. Raw value type: {g.RawValue?.GetType().Name ?? "null"}"
                            : "Goal parameter is uninitialized (%goal% not in scope when builder.validateResponse ran)");
            }
            return Task.FromResult(App.Data.@this.FromError(
                new Errors.ActionError(string.Join("; ", problems), "ValidationError", 400)));
        }
        return Task.FromResult(Validate(response, goal));
    }

    /// <summary>
    /// Public so SaveGoal can re-run validation as a safety net before persisting.
    /// Builds a fresh BuildResponse from the goal's current Steps and validates.
    /// </summary>
    public static App.Data.@this ValidateGoalState(Goal goal)
    {
        var response = new BuildResponse
        {
            Steps = goal.Steps.Select(s => new Step
            {
                Index = s.Index,
                Text = s.Text,
                Keep = false,  // post-build state: every step must stand on its own
            }.With(target => CopyActionsIfAny(s, target))).ToList()
        };
        return Validate(response, goal);
    }

    private static Step CopyActionsIfAny(Step from, Step to)
    {
        // CopyActionsIfAny lets the SaveGoal safety net see the same Actions the
        // store would persist — without mutating the source step's ownership.
        foreach (var a in from.Actions) to.Actions.Add(a);
        return to;
    }


    private static App.Data.@this Validate(BuildResponse response, Goal goal)
    {
        // Auto-fill missing indexes with keep:true placeholders when prior has actions.
        // The LLM sometimes drops a step entirely (omits its index) when it intends
        // "reuse what you had". We synthesize the placeholder so validation passes
        // and enrichResponse copies actions from the prior in-memory step.
        var presentIndexes = new HashSet<int>(response.Steps.Select(s => s.Index));
        for (int i = 0; i < goal.Steps.Count; i++)
        {
            if (presentIndexes.Contains(i)) continue;
            if (goal.Steps[i].Actions.Count == 0) continue;  // nothing to carry forward
            response.Steps.Add(new Step { Index = i, Keep = true });
        }
        response.Steps.Sort((a, b) => a.Index.CompareTo(b.Index));

        var errors = new List<string>();

        if (response.Steps.Count != goal.Steps.Count)
            errors.Add($"Step count: returned {response.Steps.Count}, expected {goal.Steps.Count}. " +
                "Steps are lines starting with '- '. Return exactly one result per goal step, with one or more actions per step.");

        var indexes = new List<int>();
        foreach (var step in response.Steps)
        {
            indexes.Add(step.Index);

            // keep:true short-circuit — prior actions already on the goal step, enrichResponse
            // copies them onto the response step. The only invariant: prior must have actions
            // to keep; otherwise there's nothing to carry forward.
            if (step.Keep)
            {
                if (step.Index >= 0 && step.Index < goal.Steps.Count
                        && goal.Steps[step.Index].Actions.Count == 0)
                    errors.Add($"Step[{step.Index}]: has keep:true but the prior .pr has no actions to keep. Emit a full mapping instead.");
                continue;
            }

            if (step.Actions.Count == 0)
                errors.Add($"Step[{step.Index}]: no actions. Every step must have at least one action with module and action.");
        }

        // Indexes must be 0..N-1 with no gaps.
        if (indexes.Count == response.Steps.Count)
        {
            var sorted = indexes.OrderBy(x => x).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i] != i)
                {
                    var expected = string.Join(",", Enumerable.Range(0, response.Steps.Count));
                    var actual = string.Join(",", indexes);
                    errors.Add($"Step indexes must be 0..{response.Steps.Count - 1} with no gaps. Expected [{expected}], got [{actual}].");
                    break;
                }
            }
        }

        // Scalar PlangType shape check — a parameter typed as a Scalar (e.g. tstring,
        // path) must carry a primitive value, never a record. The LLM sometimes wraps
        // tstring values as `{value, key}` (semantic from "translatable"); rejecting
        // the wrong shape forces LlmFixer to retry until the LLM produces the bare
        // string. The catalog teaches the correct shape — this enforces it.
        foreach (var step in response.Steps)
        {
            if (step.Keep) continue;
            foreach (var a in step.Actions)
            {
                if (a.Parameters == null) continue;
                foreach (var p in a.Parameters)
                {
                    if (p.Type?.Value == null || p.Value == null) continue;

                    var targetType = App.Utils.TypeMapping.GetType(p.Type.Value);
                    if (targetType == null) continue;
                    if (!App.Utils.TypeMapping.IsScalarPlangType(targetType)) continue;

                    if (p.Value is not string)
                        errors.Add(
                            $"Step[{step.Index}] {a.Module}.{a.ActionName}: parameter '{p.Name}' has type '{p.Type.Value}' but value is not a plain string. " +
                            $"Scalar types (e.g. tstring, path) must be emitted as bare string values, not records like {{value, key}}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Select(e => $"- {e}"));
            return App.Data.@this.FromError(new Errors.ActionError(message, "ValidationErrors", 400));
        }

        return App.Data.@this.Ok(true);
    }
}

internal static class StepValidationExt
{
    public static T With<T>(this T self, System.Action<T> mutate) { mutate(self); return self; }
}
