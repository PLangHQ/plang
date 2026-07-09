using Goal = app.goal.@this;

namespace app.module.build;

/// <summary>
/// Action wrapper: validates the structural integrity of an LLM build response.
/// The check itself lives on <see cref="BuildResponse.Validate"/> (behavior on the
/// owner) — this action just resolves its two params and delegates. SaveGoal
/// re-runs the same validation via <see cref="BuildResponse.FromGoalState"/>.
/// </summary>
[Action("validateResponse")]
public partial class validateResponse : IContext
{
    /// <summary>The LLM response. Framework deserializes the raw JsonElement to BuildResponse.</summary>
    [IsNotNull]
    public partial data.@this<BuildResponse> StepResults { get; init; }

    /// <summary>The goal being built — used to verify step count and prior actions.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.clr.@this<Goal>> Goal { get; init; }

    public async Task<app.data.@this> Run()
    {
        var response = (await StepResults.Value()) as BuildResponse;
        var goal = Goal.Clr<Goal>();

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
                            ? $"StepResults.Value is null but Data was initialized — deserialization to BuildResponse returned null. Raw value type: {sr.Peek()?.GetType().Name ?? "null"}"
                            : "StepResults parameter is uninitialized (LLM call wrote null/missing %stepResults%)");
            }
            if (goal == null)
            {
                var g = Goal;
                problems.Add(
                    g == null
                        ? "Goal parameter not bound (Data is null)"
                        : g.IsInitialized
                            ? $"Goal.Value is null but Data was initialized. Raw value type: {g.Peek()?.GetType().Name ?? "null"}"
                            : "Goal parameter is uninitialized (%goal% not in scope when builder.validateResponse ran)");
            }
            return Context.Error(
                new global::app.error.ActionError(string.Join("; ", problems), "ValidationError", 400));
        }
        return await response!.Validate(goal!, Context.App);
    }
}
