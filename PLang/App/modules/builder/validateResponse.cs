namespace App.modules.builder;

/// <summary>
/// Validates the structural integrity of an LLM build response.
/// Collects ALL errors (step count, indexes, missing actions, unknown modules)
/// and returns them in a structured message so LlmFixer can show them to the LLM.
/// </summary>
[Action("validateResponse")]
public partial class validateResponse : IContext
{
    /// <summary>The LLM response containing steps with index, actions, etc.</summary>
    public partial App.Data.@this StepResults { get; init; }

    /// <summary>The goal being built — used to verify step count.</summary>
    public partial App.Data.@this Goal { get; init; }

    public Task<App.Data.@this> Run()
    {
        var stepResults = StepResults.Value;
        var goal = Goal.Value as Goals.Goal.@this;

        if (stepResults == null || goal == null)
            return Task.FromResult(App.Data.@this.FromError(
                new Errors.ActionError("StepResults or Goal is null", "ValidationError", 400)));

        // Navigate to .steps
        List<object>? steps = null;
        if (stepResults is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (je.TryGetProperty("steps", out var stepsEl))
                steps = System.Text.Json.JsonSerializer.Deserialize<List<object>>(stepsEl.GetRawText());
        }
        else if (stepResults is IDictionary<string, object?> dict && dict.TryGetValue("steps", out var stepsObj))
        {
            if (stepsObj is List<object> lo) steps = lo;
            else if (stepsObj is IList<object> ilo) steps = ilo.ToList();
        }

        if (steps == null)
            return Task.FromResult(App.Data.@this.FromError(
                new Errors.ActionError("Could not find 'steps' in LLM response", "ValidationError", 400)));

        var errors = new List<string>();

        // Check step count
        if (steps.Count != goal.Steps.Count)
            errors.Add($"Step count: returned {steps.Count}, expected {goal.Steps.Count}. " +
                "Steps are lines starting with '- '. Return exactly one result per goal step.");

        // Validate indexes and collect per-step errors
        var indexes = new List<int>();
        for (int i = 0; i < steps.Count; i++)
        {
            int? index = null;
            List<object>? actions = null;

            if (steps[i] is System.Text.Json.JsonElement stepEl)
            {
                if (stepEl.TryGetProperty("index", out var idxProp))
                    index = idxProp.GetInt32();
                if (stepEl.TryGetProperty("actions", out var actProp) && actProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    actions = System.Text.Json.JsonSerializer.Deserialize<List<object>>(actProp.GetRawText());
            }
            else if (steps[i] is IDictionary<string, object?> stepDict)
            {
                if (stepDict.TryGetValue("index", out var idxObj) && idxObj != null)
                    index = Convert.ToInt32(idxObj);
                if (stepDict.TryGetValue("actions", out var actObj))
                    actions = actObj as List<object>;
            }

            if (index == null)
                errors.Add($"Step[{i}]: missing 'index' field");
            else
                indexes.Add(index.Value);

            if (actions == null || actions.Count == 0)
                errors.Add($"Step[{index ?? i}]: no actions. Every step must have at least one action with module and action.");
        }

        // Check indexes are 0..N-1
        if (indexes.Count == steps.Count)
        {
            var sorted = indexes.OrderBy(x => x).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i] != i)
                {
                    var expected = string.Join(",", Enumerable.Range(0, steps.Count));
                    var actual = string.Join(",", indexes);
                    errors.Add($"Step indexes must be 0..{steps.Count - 1} with no gaps. Expected [{expected}], got [{actual}].");
                    break;
                }
            }
        }

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Select(e => $"- {e}"));
            return Task.FromResult(App.Data.@this.FromError(
                new Errors.ActionError(message, "ValidationErrors", 400)));
        }

        return Task.FromResult(App.Data.@this.Ok(true));
    }
}
