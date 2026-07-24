using app.data;
using Action = app.goal.step.action.@this;
using System.Collections.Generic;
using System.Linq;

namespace app.goal.step.action.list;

/// <summary>
/// The action NODE — a step's actions (<c>step.Action</c>), a plang list value (<c>list&lt;action&gt;</c>)
/// so a reader returns it directly and it flows as ONE type end to end (no build-then-wrap). PROGRAM
/// STRUCTURE: born context-free (the graph is shared across concurrent runs), it stores no context —
/// <see cref="Run"/> takes the ASK's. Keeps the node's own fire-or-fall-through <see cref="Run"/> and
/// the coverage-key <see cref="IndexOf"/> (items are responsible for themselves — Rule 5). Twin of
/// <see cref="app.goal.step.list.@this"/>.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<Action>
{
    // Empty program node (field defaults / an emptied chain) — context-free, filled by the reader.
    public @this() : base(new List<object?>()) { }
    // The elements ride as typed items in the backing (store raw, type on read); no context stamped.
    public @this(IEnumerable<Action> actions) : base(new List<object?>(actions.Cast<object?>())) { }

    /// <summary>Clone/render keep this concrete node type, context-free.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this();

    /// <summary>Runs the chain: setup / non-condition actions dispatch in order; a condition evaluates,
    /// and if truthy runs its <c>Child</c> branch and stops (the rest of the chain — elseif/else — is
    /// skipped). An ordinary action has an empty Child so never enters. Context is the ASK's, handed to
    /// each action's own Run.</summary>
    public async System.Threading.Tasks.Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var action in Elements)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            result = await action.Run(context);
            if (result.ShouldExit() || result.Handled) break;         // return/exit, or a legit event-handled stop
            if (action.IsCondition && await result.ToBooleanAsync())
            {
                result = await action.Child.Run(context);             // gate fired → run the branch body
                break;                                                // branch taken → skip the rest of the chain
            }
        }
        return result;
    }

    /// <summary>The coverage key — an action's index by reference identity (the same instance the
    /// chain ran), or -1 when absent.</summary>
    public int IndexOf(Action a)
    {
        var els = Elements;
        for (int i = 0; i < els.Count; i++)
            if (ReferenceEquals(els[i], a)) return i;
        return -1;
    }
}
