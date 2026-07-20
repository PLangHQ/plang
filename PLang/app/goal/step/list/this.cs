using app.data;
using System.Collections.Generic;

namespace app.goal.step.list;

/// <summary>
/// The step NODE — a goal's steps (`goal.Step`) or a control-flow action's branch body
/// (`action.Child`). Typed storage that owns its sequence-run (Rule 5 — the collection owns its
/// iteration). `[i]`/`list` reach the collection; `.current` (the running step) is a nav derivation
/// from the callstack, not node state, so it lives at the nav boundary, not here.
/// </summary>
public sealed class @this
{
    // Storage is a List reused-not-copied when the caller already has one (the reader/parser build a
    // List then wrap; the parser mutates it after wrapping — the node must see those adds). A genuine
    // IReadOnlyList (a `.list` copy target) is wrapped once. The graph is read-only after load; Add is
    // a construction affordance (builder/tests), never called on a live goal.
    private readonly List<Step> _steps;
    public @this(IReadOnlyList<Step> steps) => _steps = steps as List<Step> ?? new List<Step>(steps);

    public Step this[int i] => _steps[i];                       // goal.step[0]
    public IReadOnlyList<Step> list => _steps;                 // goal.step.list  (IEnumerable → list kind → navigable)
    public int Count => _steps.Count;
    public void Add(Step step) => _steps.Add(step);            // construction only

    /// <summary>Runs the steps in sequence. A return / exit propagates up (ShouldExit folds Returned).
    /// No indent skip-state — a fired control-flow action runs its own Child, so nesting is structural.</summary>
    public async System.Threading.Tasks.Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var step in _steps)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new global::app.error.Error("Operation was cancelled", "Cancelled", 499));
            result = await step.Run(context);
            if (result.ShouldExit()) break;
        }
        return result;
    }
}
