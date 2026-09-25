namespace app.goal.step;

// The step judges itself through its action chain — the chain's verdict is the step's cause, and
// the step adds only what it alone knows: which step it is.
public sealed partial class @this
{
    /// <summary>What is wrong with this step, or null when nothing is. The verdict keeps its cause's
    /// key — an `on error key "ElseWithoutIf"` sees what went wrong, not only that a step did.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(
        global::app.actor.context.@this context)
    {
        if (await Code.Validate(context) is not { } invalid) return null;
        return new global::app.error.StepError(
            $"step {Index} '{Text}': {invalid.Message}", this, invalid.Key, invalid.StatusCode) { list = { invalid } };
    }
}
