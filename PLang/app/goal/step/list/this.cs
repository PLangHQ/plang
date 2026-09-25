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
    // Two ways to be born: EMPTY (callers Add each step in — the reader, the parser, the fold), or
    // ADOPT a list value's rows (the value→slot materialization when a reader produced a generic list).
    public @this() : base(new List<object?>()) { }
    public @this(global::app.type.item.list.@this source) : base(source) { }

    /// <summary>Clone/render keep this concrete node type, context-free.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this();

    /// <summary>Runs the steps in sequence. A return / exit propagates up (ShouldExit folds Returned).
    /// No indent skip-state — a fired control-flow action runs its own Child, so nesting is structural.
    /// Context is the ASK's, handed to each step's own Run. The node iterates ITSELF (the typed
    /// positional face).</summary>
    public async System.Threading.Tasks.Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        for (int i = 0; i < Count; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new global::app.error.Error("Operation was cancelled", "Cancelled", 499));
            result = await this[i].Run(context);
            if (result.ShouldExit()) break;
        }
        return result;
    }

    /// <summary>Judges a stage-3 answer's entries against these steps, before any step takes its
    /// actions: exactly one entry per step, in order, entry i labelled <c>"index": i</c>, none without
    /// actions — so the .pr matches the .goal line for line. Null when it matches; otherwise one error
    /// naming every mismatch, so the correction prompt sees all of them at once.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Match(
        global::app.type.item.list.@this entries, actor.context.@this context)
    {
        var rows = entries.Items(context).ToList();
        var steps = CountRaw;
        var problems = new List<string>();
        for (int i = 0; i < System.Math.Max(steps, rows.Count); i++)
        {
            if (i >= rows.Count) { problems.Add($"step {i} (\"{this[i].Text}\") has no entry"); continue; }
            if (i >= steps) { problems.Add($"entry {i} is extra: the goal has {steps} steps"); continue; }

            var entry = await rows[i].Value<global::app.type.item.dict.@this>();
            if (entry == null) { problems.Add($"entry {i} is not an object"); continue; }

            var index = entry.Get("index", context) is { } label
                ? await label.Value<global::app.type.item.number.@this>() : null;
            if (index == null) problems.Add($"entry {i} has no index");
            else if (index.ToInt64() != i) problems.Add($"entry {i} is labelled index {index.ToInt64()}");

            var actions = entry.Get("action", context) is { } held
                ? await held.Value<global::app.type.item.list.@this>() : null;
            if (actions == null || actions.CountRaw == 0) problems.Add($"step {i} (\"{this[i].Text}\") has no actions");
        }
        if (problems.Count == 0) return null;
        return new global::app.error.Error(
            $"The answer does not match the goal's {steps} steps: {string.Join("; ", problems)}. " +
            "Answer exactly one entry per step, in order — entry i with \"index\": i — each with its actions.",
            "AnswerMismatch", 400);
    }

    /// <summary>Writes itself to the wire as the bare step array — each element writes its own step
    /// shape (NOT the base's Data-envelope value face). Holders say <c>Step.Output(...)</c>.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginArray((int)Count);
        for (int i = 0; i < Count; i++) await this[i].Output(writer, mode, context);
        writer.EndArray();
    }
}
