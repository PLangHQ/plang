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
    // Two ways to be born: EMPTY (callers Add each action in — the reader, Nest), or ADOPT a list
    // value's rows (the value→slot materialization, set %step.action% = %json%).
    public @this() : base(new List<object?>()) { }
    public @this(global::app.type.item.list.@this source) : base(source) { }

    /// <summary>Clone/render keep this concrete node type, context-free.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this();

    /// <summary>Runs the chain: setup / non-condition actions dispatch in order; a condition evaluates,
    /// and if truthy runs its <c>Child</c> branch and stops (the rest of the chain — elseif/else — is
    /// skipped). An ordinary action has an empty Child so never enters. Context is the ASK's, handed to
    /// each action's own Run. The node iterates ITSELF (the typed positional face), never a harvested
    /// element list.</summary>
    public async System.Threading.Tasks.Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        for (int i = 0; i < Count; i++)
        {
            var action = this[i];
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

    /// <summary>Writes itself to the wire as the bare <c>.pr</c> action array — each element writes its
    /// own action shape (NOT the base list's self-describing Data-envelope value face). The holder just
    /// says <c>Action.Output(...)</c>; the node is the iterator of itself, like <see cref="Run"/>.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginArray((int)Count);
        for (int i = 0; i < Count; i++) await this[i].Output(writer, mode, context);
        writer.EndArray();
    }

    /// <summary>The coverage key — an action's index by reference identity (the same instance the
    /// chain ran), or -1 when absent.</summary>
    public int IndexOf(Action a)
    {
        for (int i = 0; i < Count; i++)
            if (ReferenceEquals(this[i], a)) return i;
        return -1;
    }
}
