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
    /// <summary>
    /// Runs all steps in order. Owns the iteration loop (OBP rule 5).
    /// Sub-step logic: indented steps default to NOT executing.
    /// A condition step returning bool true "proves" its children,
    /// allowing indented steps below it to execute.
    /// All sub-step state is local — fully thread-safe.
    /// </summary>
    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        Data? lastResult = null;
        // Sub-step tracking: indented steps don't execute unless proven true.
        // When a step has indented children and its result is NOT bool true,
        // skipBelowIndent is set to that step's indent, skipping deeper steps.
        int? skipBelowIndent = null;

        for (var i = 0; i < Count; i++)
        {
            var step = this[i];

            // Sub-step skip: if we're skipping indented children, check indent level
            if (skipBelowIndent != null)
            {
                if (step.Indent > skipBelowIndent)
                    continue;
                // Back to parent level or shallower — stop skipping
                skipBelowIndent = null;
            }

            // Setup run-once check: skip steps that have already been executed
            if (context.Setup != null && await context.Setup.IsExecuted(step, engine))
                continue;

            var stepResult = await step.RunAsync(engine, context, cancellationToken);

            // Sub-step control: indented steps default to NOT executing.
            // A condition step proves its children by setting __condition__ = true in memory.
            // Only condition steps (those that set __condition__) can trigger sub-step skipping.
            // Non-condition steps with indented children are ignored — children always execute.
            if (HasIndentedChildren(i))
            {
                var conditionSignal = context.MemoryStack.Get("__condition__");
                if (conditionSignal != null)
                {
                    context.MemoryStack.Remove("__condition__");
                    if (conditionSignal.Value is not true)
                        skipBelowIndent = step.Indent;
                }
            }

            // Determine if the step error is tolerated:
            // - Success (no error)
            // - Explicit IgnoreError on the step
            // - Setup-tolerable errors (e.g. "already exists", "duplicate column name")
            var errorTolerated = stepResult.Success
                || (step.OnError?.IgnoreError ?? false)
                || (context.Setup != null && context.Setup.IsTolerableError(stepResult));

            // Record in setup table only on success or tolerated errors.
            // Failed steps that abort setup must NOT be recorded — they need to re-run on next startup.
            // Recording failure aborts setup — if we can't track execution, re-running is safer than skipping.
            if (context.Setup != null && errorTolerated)
            {
                var recordResult = await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
                if (!recordResult.Success)
                    return recordResult;
            }

            if (!stepResult && !errorTolerated)
                return stepResult;

            if (cancellationToken.IsCancellationRequested)
                return Data.FromError(GoalError.Cancelled(context));

            lastResult = stepResult;
        }

        return lastResult ?? Data.Ok();
    }

    /// <summary>
    /// Returns true if the step at the given index has a following step with higher indent.
    /// </summary>
    public bool HasIndentedChildren(int index)
    {
        return index + 1 < Count && this[index + 1].Indent > this[index].Indent;
    }
}
