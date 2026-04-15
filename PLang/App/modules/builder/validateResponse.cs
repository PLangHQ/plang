namespace App.modules.builder;

/// <summary>
/// Validates the structural integrity of an LLM build response.
/// Checks: step count matches goal, indexes are 0..N-1 with no gaps/duplicates.
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

        // Navigate to .steps — the LLM response is resolved as a dictionary by variable resolution
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

        // Check step count
        if (steps.Count != goal.Steps.Count)
            return Task.FromResult(App.Data.@this.FromError(
                new Errors.ActionError(
                    $"LLM returned {steps.Count} steps but goal has {goal.Steps.Count} steps. " +
                    "Steps are lines starting with '- '. Return exactly one result per goal step.",
                    "StepCountMismatch", 400)));

        // Validate indexes: collect all, check for 0..N-1
        var indexes = new List<int>();
        for (int i = 0; i < steps.Count; i++)
        {
            int? index = null;
            if (steps[i] is System.Text.Json.JsonElement stepEl && stepEl.TryGetProperty("index", out var idxProp))
                index = idxProp.GetInt32();
            else if (steps[i] is IDictionary<string, object?> stepDict && stepDict.TryGetValue("index", out var idxObj))
                index = Convert.ToInt32(idxObj);

            if (index == null)
            {
                return Task.FromResult(App.Data.@this.FromError(
                    new Errors.ActionError(
                        $"Step at position {i} is missing 'index' field.",
                        "MissingIndex", 400)));
            }

            indexes.Add(index.Value);
        }

        var sorted = indexes.OrderBy(x => x).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i] != i)
            {
                var expected = string.Join(",", Enumerable.Range(0, steps.Count));
                var actual = string.Join(",", indexes);
                return Task.FromResult(App.Data.@this.FromError(
                    new Errors.ActionError(
                        $"Step indexes must be 0..{steps.Count - 1} with no gaps. Expected [{expected}], got [{actual}].",
                        "InvalidIndexes", 400)));
            }
        }

        return Task.FromResult(App.Data.@this.Ok(true));
    }
}
