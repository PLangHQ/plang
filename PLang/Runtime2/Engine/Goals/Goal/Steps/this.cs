using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Goals.Goal.Steps;

public sealed class @this : List<Step.@this>
{
    public @this() { }
    public @this(IEnumerable<Step.@this> steps) : base(steps) { }

    public List<Step.@this> Value => this;

    public async Task<Data> Load(PLangContext context)
    {
        foreach (var step in this)
        {
            var result = await step.Load(context);
            if (!result.Success) return result;
        }
        return Data.Ok();
    }

    /// <summary>
    /// Runs all steps in order. Owns the iteration loop (OBP rule 5).
    /// When context.Setup is set, implements run-once semantics:
    /// skips already-executed steps and records new executions.
    /// </summary>
    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < Count; i++)
        {
            var step = this[i];

            // Setup run-once check: skip steps that have already been executed
            if (context.Setup != null && await context.Setup.IsExecuted(step, engine))
                continue;

            var stepResult = await step.RunAsync(engine, context, cancellationToken);

            // Record in setup table only on success or tolerated errors.
            // Failed steps that abort setup must NOT be recorded — they need to re-run on next startup.
            if (context.Setup != null)
            {
                var tolerated = stepResult.Success || (step.OnError?.IgnoreError ?? false);
                if (tolerated)
                    await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
            }

            if (!stepResult)
            {
                if (!(step.OnError?.IgnoreError ?? false))
                    return stepResult;
            }

            if (cancellationToken.IsCancellationRequested)
                return Data.FromError(GoalError.Cancelled(context));
        }

        return Data.Ok();
    }
}
