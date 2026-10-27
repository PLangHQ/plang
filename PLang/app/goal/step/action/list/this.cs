using app.data;
using Action = app.goal.step.action.@this;
using System.Collections.Generic;
using System.Linq;

namespace app.goal.step.action.list;

/// <summary>
/// The action NODE — a step's actions (<c>step.Action</c>), a plang list value (<c>list&lt;action&gt;</c>)
/// so a reader returns it directly and it flows as ONE type end to end (no build-then-wrap). PROGRAM
/// STRUCTURE: born context-free (the graph is shared across concurrent runs), it stores no context —
/// <see cref="Start"/> takes the ASK's. Keeps the node's own fire-or-fall-through <see cref="Start"/> and
/// the coverage-key <see cref="IndexOf"/> (items are responsible for themselves — Rule 5). Twin of
/// <see cref="app.goal.step.list.@this"/>.
/// </summary>
// formal — the actions written as calls — is a format of list (a list of actions is what its reader makes), so the
// kind is {list, formal}; its encode and decode are this class's: decoding formal makes an action list
[global::app.Attributes.Format(global::app.goal.step.action.formal.Writer.Token, Text = true)]
public sealed class @this : global::app.type.item.list.bare.@this<Action>, global::app.type.item.ICreate<@this>
{
    // Two ways to be born: EMPTY (callers Add each action in — the reader, Nest), or ADOPT a list
    // value's rows (the value→slot materialization, set %step.action% = %json%).
    public @this(global::app.type.@this type) : base(new List<object?>(), type) { }
    public @this(global::app.type.item.list.@this source, global::app.type.@this type) : base(source, type) { }

    /// <summary>Starts the chain from <paramref name="from"/> (0: the whole chain; a resume, where the run stopped or
    /// after it): setup / non-condition actions take their turn in order (a clause leaves the chain's result as it
    /// stands); a condition evaluates, and if truthy starts its <c>Child</c> branch and stops (the rest of the chain —
    /// elseif/else — is skipped). An ordinary action has an empty Child so never enters. Context is the ASK's, handed to
    /// each action's own Start. A program's error that travels as an exception is the chain's answer, whole. The node
    /// iterates ITSELF (the typed positional face), never a harvested element list. A resume after an action hands in
    /// that action's <paramref name="result"/>, the chain's result as it stood, so the actions after it read it as they
    /// would have; with nothing left to start, it is the chain's answer.</summary>
    public async System.Threading.Tasks.Task<data.@this> Start(actor.context.@this context, int from = 0, data.@this? result = null)
    {
        result ??= context.Ok();
        try
        {
            for (int i = from; i < Count; i++)
            {
                var action = this[i];
                context.CancellationToken.ThrowIfCancellationRequested();
                result = await action.Follow(result, context);
                if (result.ShouldExit() || result.Handled) break;         // return/exit, or a legit event-handled stop
                if (action.IsCondition && await result.ToBooleanAsync())
                {
                    result = await action.Child.Start(context);           // gate fired → start the branch body
                    break;                                                // branch taken → skip the rest of the chain
                }
            }
        }
        catch (global::app.error.AppException ex) { result = context.Error(ex.Error); }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException or System.OperationCanceledException))
        {
            result = context.Error(global::app.error.@this.FromException(ex));
        }
        return result;
    }

    /// <summary>The actions this code runs as its own (<c>module.action</c>): top level with its clauses, a
    /// condition's body — not an action held as a value, nor a clause's recovery.</summary>
    public IEnumerable<string> Own
    {
        get
        {
            foreach (var a in Items())
            {
                yield return $"{a.Module.Name}.{a.Name}";
                foreach (var child in a.Child.Items())
                    foreach (var name in child.Code.Own) yield return name;
            }
        }
    }

    /// <summary>Attaches each action to the step action it follows (<c>read x; on.error(…); on.cache(…)</c> — both
    /// clauses on read) — what the program's read does once its code is in. Clauses are attached for every actor
    /// running the program, and never started as steps.</summary>
    public void Attach()
    {
        Action? before = null;
        foreach (var action in Items())
        {
            action.Attach(before);
            before = action.Anchor(before);
        }
    }

    /// <summary>What is wrong with this chain, or null when nothing is. The node iterates ITSELF and
    /// each action judges itself, the same shape as <see cref="Start"/>; the causes of every element
    /// gather into one error for the chain.
    /// <para>An EMPTY chain is the list's own verdict, not a pass: a step maps to at least one
    /// action, and the list is the only thing that can see there is nothing to judge.</para></summary>
    public async System.Threading.Tasks.Task<global::app.error.@this?> Validate(actor.context.@this context)
    {
        if (Count == 0)
            return new global::app.error.@this(
                "the compiled step has no actions — every step maps to at least one action.",
                "EmptyActions", 400);

        if (Chain() is { } broken) return broken;

        var causes = new List<global::app.error.@this>();
        for (int i = 0; i < Count; i++)
            if (await this[i].Validate(context) is { } invalid) causes.Add(invalid);

        if (causes.Count == 0) return null;
        return new global::app.error.@this(
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
    private global::app.error.@this? Chain()
    {
        global::app.goal.step.action.@this? condition = null;   // the last condition in this list
        global::app.goal.step.action.@this? anchor = null;      // the step action what follows belongs to — a clause's
        global::app.error.@this? missing = null;                 // a branch with no body — reported last
        for (int i = 0; i < Count; i++)
        {
            var action = this[i];
            // what the actions so far belong to: a clause belongs to the step action before it
            var before = anchor;
            var belongs = action.Belongs(anchor);
            anchor = action.Anchor(anchor);
            if (action.IsBranch)
            {
                if (before is not { IsCondition: true } || string.Equals(before.Name, "else", System.StringComparison.OrdinalIgnoreCase))
                    return Broken(action, $"an {action.Name} must be in the same step as its if.", "ElseWithoutIf");
            }

            if (action.IsCondition)
            {
                // `{ }` written empty is a child of no actions; no `{ }` at all is no child
                var empty = action.Child.Count > 0 && action.Child.Items().All(c => c.Code.Count == 0);
                var below = HasBodyBelow(action);
                if (missing == null && (empty || (action.Child.Count == 0 && !below)))
                    missing = Broken(action, below
                        ? "`{ }` holds at least one action; a condition whose body is indented below it has no `{ }`."
                        : $"the step has nothing indented below it, so what the step does when the condition holds goes inside the {action.Name}'s `{{ }}`.",
                        "BodyMissing");
                condition = action;
                continue;
            }

            // what belongs to the action before it (a clause: the condition's own error) chains; a step action stands on its
            // own, so after a condition it is a body beside it
            if (condition != null && !belongs)
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
    private global::app.error.@this Broken(global::app.goal.step.action.@this action, string rule, string key)
    {
        var message = action.Step is { } step ? $"step {step.Index} \"{step.Text}\" — {rule}" : $"\"{action.Name}\" — {rule}";
        return action.Step is { } owner
            ? new global::app.error.StepError(message, owner, key, 400)
            : new global::app.error.StepError(message, key, 400);
    }

    /// <summary>Finishes every action in this chain at build, in order — each walks what it holds.
    /// Null when nothing is wrong; otherwise one error for the chain with each action's as a cause.
    /// An empty chain has nothing to build — emptiness is <see cref="Validate"/>'s verdict.</summary>
    public async System.Threading.Tasks.Task<global::app.error.@this?> Build(actor.context.@this context)
    {
        var causes = new List<global::app.error.@this>();
        Action? before = null;
        for (int i = 0; i < Count; i++)
        {
            var action = this[i];
            if (await action.Build(context) is { } failed) causes.Add(failed);
            if (action.Refuse(before) is { } refused) causes.Add(refused);
            before = action.Anchor(before);
        }

        if (causes.Count == 0) return null;
        return new global::app.error.@this(
            string.Join("; ", causes.Select(c => c.Message)), "BuildFailed", 400) { list = causes };
    }

    /// <summary>A step's actions in formal: one line, each action's own call, <c>a; b</c>.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.goal.step.action.formal.Writer writer)
    {
        writer.BeginActions();
        for (int i = 0; i < Count; i++) await this[i].Output(writer);
        writer.EndActions();
    }

    /// <summary>The actions after <paramref name="a"/> in this chain — what a loop that leads its step runs
    /// for each element. None when <paramref name="a"/> isn't in the chain or is its last.</summary>
    public IReadOnlyList<Action> After(Action a)
    {
        var at = IndexOf(a);
        return at < 0 ? [] : Items().Skip(at + 1).ToList();
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
