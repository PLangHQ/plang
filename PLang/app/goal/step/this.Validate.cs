namespace app.goal.step;

// The step judges itself through its action chain — the chain's verdict is the step's cause, and
// the step adds only what it alone knows: which step it is.
public sealed partial class @this
{
    /// <summary>What is wrong with this step, or null when nothing is.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(
        global::app.actor.context.@this context)
    {
        if (await Action.Validate(context) is not { } invalid) return null;
        return new global::app.error.StepError(
            $"step {Index} '{Text}': {invalid.Message}", this, "StepInvalid", 400) { list = { invalid } };
    }
}
