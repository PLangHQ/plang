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

    /// <summary>What is wrong with this chain, or null when nothing is. The node iterates ITSELF and
    /// each action judges itself, the same shape as <see cref="Run"/>; the causes of every element
    /// gather into one error for the chain.
    /// <para>An EMPTY chain is the list's own verdict, not a pass: a step maps to at least one
    /// action, and the list is the only thing that can see there is nothing to judge.</para></summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(actor.context.@this context)
    {
        if (Count == 0)
            return new global::app.error.Error(
                "the compiled step has no actions — every step maps to at least one action.",
                "EmptyActions", 400);

        if (Chain() is { } broken) return broken;

        var causes = new List<global::app.error.Error>();
        for (int i = 0; i < Count; i++)
            if (await this[i].Validate(context) is { } invalid) causes.Add(invalid);

        if (causes.Count == 0) return null;
        return new global::app.error.Error(
            string.Join("; ", causes.Select(c => c.Message)), "BuildValidation", 400) { list = causes };
    }

    /// <summary>The condition chain's shape in this list: actions before the first condition are the
    /// step's own; from the first <c>if</c> on, only <c>elseif</c>/<c>else</c> may follow, each right
    /// after an <c>if</c>/<c>elseif</c>. Two ways to break it, two keys:
    /// <list type="bullet">
    /// <item><c>ElseWithoutIf</c> — an elseif/else with no condition right before it (at the start of
    /// the list, after an ordinary action, after an else): the programmer's else written apart from
    /// its if, a standalone <c>- else</c> step.</item>
    /// <item><c>BodyBesideCondition</c> — an ordinary action after a condition: a branch's body put
    /// beside it instead of in its child, where it would run when the condition is false.</item>
    /// <item><c>BodyMissing</c> — a condition with an empty child and no steps indented under its step:
    /// the branch does nothing. Reported only when the chain is otherwise whole.</item>
    /// </list>
    /// ElseWithoutIf is the programmer's to fix (Settle sends it to SourceError); the other two are the
    /// answer's, and go back through FixProperties. Null when the chain is whole.</summary>
    private global::app.error.Error? Chain()
    {
        global::app.goal.step.action.@this? condition = null;   // the last condition in this list
        global::app.error.Error? missing = null;                 // a branch with no body — reported last
        for (int i = 0; i < Count; i++)
        {
            var action = this[i];
            bool continues = action.IsCondition && !string.Equals(action.Name, "if", System.StringComparison.OrdinalIgnoreCase);

            if (continues)
            {
                var before = i > 0 ? this[i - 1] : null;
                if (before is not { IsCondition: true } || string.Equals(before.Name, "else", System.StringComparison.OrdinalIgnoreCase))
                    return Broken(action, $"an {action.Name} must be in the same step as its if.", "ElseWithoutIf");
            }

            if (action.IsCondition)
            {
                if (missing == null && action.Child.Count == 0 && !HasBodyBelow(action))
                    missing = Broken(action,
                        $"the {action.Name} has no body — what the step does when it holds goes in its child.",
                        "BodyMissing");
                condition = action;
                continue;
            }

            if (condition != null)
                return Broken(action,
                    $"`{action.Module}.{action.Name}` is after the {condition.Name} — a branch's body goes in its child.",
                    "BodyBesideCondition");
        }
        return missing;
    }

    /// <summary>The action's step has steps indented under it in its goal — its body, which the builder
    /// places in the condition's child from that layout (build.fold).</summary>
    private bool HasBodyBelow(global::app.goal.step.action.@this action)
        => action.Step is { Goal: { } goal } step
           && step.Index < goal.Step.CountRaw && ReferenceEquals(goal.Step[step.Index], step)
           && goal.Step.Body(step.Index).CountRaw > 0;

    /// <summary>The chain's verdict on <paramref name="action"/>, naming its step when it holds one.</summary>
    private global::app.error.Error Broken(global::app.goal.step.action.@this action, string rule, string key)
    {
        var message = action.Step is { } step ? $"step {step.Index} \"{step.Text}\" — {rule}" : $"\"{action.Name}\" — {rule}";
        return action.Step is { } owner
            ? new global::app.error.StepError(message, owner, key, 400)
            : new global::app.error.StepError(message, key, 400);
    }

    /// <summary>Finishes every action in this chain at build, in order — each walks what it holds.
    /// Null when nothing is wrong; otherwise one error for the chain with each action's as a cause.
    /// An empty chain has nothing to build — emptiness is <see cref="Validate"/>'s verdict.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Build(actor.context.@this context)
    {
        var causes = new List<global::app.error.Error>();
        for (int i = 0; i < Count; i++)
            if (await this[i].Build(context) is { } failed) causes.Add(failed);

        if (causes.Count == 0) return null;
        return new global::app.error.Error(
            string.Join("; ", causes.Select(c => c.Message)), "BuildFailed", 400) { list = causes };
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
