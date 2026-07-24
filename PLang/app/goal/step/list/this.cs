using app.data;
using System.Collections.Generic;
using System.Linq;

namespace app.goal.step.list;

/// <summary>
/// The step NODE — a goal's steps (<c>goal.Step</c>) or a control-flow action's branch body
/// (<c>action.Child</c>), a plang list value (<c>list&lt;step&gt;</c>) so a reader returns it directly.
/// PROGRAM STRUCTURE: born context-free (the graph is shared across concurrent runs), it stores no
/// context — <see cref="Run"/> takes the ASK's. Owns its sequence-run (Rule 5 — the collection owns its
/// iteration). Twin of <see cref="app.goal.step.action.list.@this"/>.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<Step>
{
    public @this() : base(new List<object?>()) { }
    public @this(IEnumerable<Step> steps) : base(new List<object?>(steps.Cast<object?>())) { }

    /// <summary>Clone/render keep this concrete node type, context-free.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this();

    /// <summary>Runs the steps in sequence. A return / exit propagates up (ShouldExit folds Returned).
    /// No indent skip-state — a fired control-flow action runs its own Child, so nesting is structural.
    /// Context is the ASK's, handed to each step's own Run.</summary>
    public async System.Threading.Tasks.Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var step in Elements)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new global::app.error.Error("Operation was cancelled", "Cancelled", 499));
            result = await step.Run(context);
            if (result.ShouldExit()) break;
        }
        return result;
    }
}
