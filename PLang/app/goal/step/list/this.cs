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

    /// <summary>Every step is cached — built before, word for word, its code standing (none reopened).
    /// A step list that is cached has nothing to ask.</summary>
    public bool IsCached => Items().All(s => s.IsCached);

    /// <summary>The step list answers what it knows of itself (<c>%goal.Step.IsCached%</c>); any other
    /// key is a list's read.</summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
        => string.Equals(key, nameof(IsCached), System.StringComparison.OrdinalIgnoreCase)
            ? new data.@this(key, IsCached, parent: parent)
            : await base.Get(parent, key);

    /// <summary>The steps indented under the step at <paramref name="index"/> — the consecutive steps
    /// after it written deeper than it: its body as the author laid it out (the parser records each
    /// step's indent). Empty when nothing is indented under it.</summary>
    public @this Body(int index)
    {
        var body = new @this();
        if (index < 0 || index >= CountRaw) return body;
        var indent = this[index].Line.Indent;
        for (int j = index + 1; j < CountRaw && this[j].Line.Indent > indent; j++) body.Add(this[j]);
        return body;
    }

    // A step's line in the answer: `[i]` at the start of a line.
    private static readonly System.Text.RegularExpressions.Regex Head =
        new(@"^\[(\d+)\][ \t]*", System.Text.RegularExpressions.RegexOptions.Multiline);

    // An action a line names (module.name( …) — a line that doesn't read is still checked for them.
    private static readonly System.Text.RegularExpressions.Regex Named = new(@"\b([a-z]+)\.([A-Za-z_]+)\(");

    /// <summary>
    /// Reads the stage-3 answer — one line per step in formal, <c>[i] module.action(…); …</c> — into the
    /// steps still open (a step without code). Each open step's line is read and checked: it reads
    /// (formal), it drops what it didn't need to write, a body it wrote over the steps indented under it
    /// is a copy (dropped) or invented (refused), its chain is whole, its actions and properties judge
    /// themselves, it agrees with the decider's picks, and it holds what its words say. A step that
    /// passes takes its code (the warnings it builds with, its unset defaults frozen); a step already
    /// holding code keeps it —
    /// a retry answers only the refused steps. Null when every step has code; otherwise one error
    /// holding each refused step's problems, every one at once, and the steps under Details["steps"].
    /// </summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Read(string answer, actor.context.@this context)
    {
        var whole = new List<string>();
        var heads = Head.Matches(answer);
        // an empty answer is whole: a step it leaves unanswered has no entry (one written in formal needs none)
        if (answer.Trim().Length > 0 && (heads.Count == 0 || answer[..heads[0].Index].Trim().Length > 0))
            whole.Add("each step's line starts with its index: [0] action; action");
        var lines = new Dictionary<int, string>();
        for (int n = 0; n < heads.Count; n++)
        {
            var i = int.Parse(heads[n].Groups[1].Value);
            var end = n + 1 < heads.Count ? heads[n + 1].Index : answer.Length;
            var line = answer[(heads[n].Index + heads[n].Length)..end].TrimEnd();
            if (i >= CountRaw) whole.Add($"entry {i} is extra: the goal has {CountRaw} steps");
            else if (!lines.TryAdd(i, line)) whole.Add($"step [{i}] is answered twice");
        }

        var refused = new SortedDictionary<int, List<string>>();
        void Refuse(int i, string why) { if (!refused.TryGetValue(i, out var p)) refused[i] = p = new(); p.Add(why); }
        string? key = null;

        // Each open step's line, read.
        var read = new Dictionary<int, global::app.goal.step.action.list.@this>();
        for (int i = 0; i < CountRaw; i++)
        {
            var step = this[i];
            if (step.Code.Count > 0)
            {
                if (lines.ContainsKey(i))
                    await (context.App.Debug?.Write($"build.match: step {i} already has its code; its line in the answer is set aside") ?? System.Threading.Tasks.Task.CompletedTask);
                continue;
            }
            // a step written in formal is its own line: read as written, whatever the answer says
            string? line = step.IsFormal ? step.Text : null;
            if (line == null && !lines.TryGetValue(i, out line)) { Refuse(i, $"step {i} (\"{step.Text}\") has no entry"); continue; }
            var formal = new global::app.goal.step.action.serializer.Formal(step).Read(line, context);
            if (!formal.Success && step.IsFormal)
            {
                Refuse(i, $"step {i} is written in formal and does not read: {formal.Error!.FixSuggestion ?? formal.Error.Message}");
                continue;
            }
            if (!formal.Success)
            {
                Refuse(i, $"step {i} does not parse: {formal.Error!.FixSuggestion ?? formal.Error.Message} — in: [{i}] {line.Trim()}");
                var named = Named.Matches(line).Select(m => (m.Groups[1].Value, m.Groups[2].Value))
                    .Where(a => context.App.Module.Contains(a.Item1) && context.App.Module[a.Item1][a.Item2] != null)
                    .Select(a => $"{a.Item1}.{a.Item2}");
                foreach (var why in step.Pick.Unlisted(named)) Refuse(i, why);
                continue;
            }
            var actions = (global::app.goal.step.action.list.@this)formal.Peek()!;
            foreach (var action in actions.Items()) action.Reduce();
            read[i] = actions;
        }

        // Each read step, checked — every problem it shows, at once — in the goal's order, walked over
        // one scratch store: a step reads the variables the steps before it left.
        using var scratch = Scratch(context);
        for (int i = 0; i < CountRaw; i++)
        {
            var step = this[i];
            if (!read.TryGetValue(i, out var actions)) { await step.Scope(scratch); continue; }
            // A step with steps indented under it gets its body from that layout (build.fold places it):
            // a body written over it that holds only what the indented steps do is a copy, dropped; one
            // that holds anything else is invented.
            var body = Body(i);
            if (body.CountRaw > 0)
            {
                var below = body.Items().SelectMany(b => (read.TryGetValue(b.Index, out var r) ? r : b.Code).Own).ToList();
                foreach (var action in actions.Items().Where(a => a.Child.Count > 0))
                {
                    var inside = action.Child.Items().SelectMany(c => c.Code.Own).ToList();
                    var spare = below.ToList();
                    if (inside.All(spare.Remove)) action.Child = new @this();
                    else Refuse(i, $"step {i}'s body is the steps indented under it ({string.Join(", ", body.Items().Select(b => b.Index))}); " +
                                   $"the builder places them — write step {i} without {{ }} and each indented step on its own line");
                }
            }
            step.Code = actions;
            var invalid = await step.Validate(context);
            if (invalid != null)
            {
                key ??= invalid.Key == "ElseWithoutIf" ? invalid.Key : null;
                foreach (var cause in invalid.list) Refuse(i, $"step {i} (\"{step.Text}\") — {cause.Message}");
            }
            // the answer holds what the step's words say — checked on the answer as written, before the
            // handlers' Build may change it
            foreach (var uncovered in await step.Cover(context)) Refuse(i, $"step {i} (\"{step.Text}\") — {uncovered}");
            // only code that judged itself sound is built
            if (invalid == null && await actions.Build(context) is { } failed)
                foreach (var cause in failed.list) Refuse(i, $"step {i} (\"{step.Text}\") — {cause.Message}");
            // a step written in formal was asked of no decider: there are no picks to agree with
            var (disagree, warnings) = step.IsFormal
                ? (new List<string>(), new List<global::app.warning.@this>()) : step.Pick.Agree(actions);
            foreach (var why in disagree) Refuse(i, why);
            foreach (var declined in await step.Scope(scratch)) Refuse(i, $"step {i} (\"{step.Text}\") — {declined.Message}");
            if (refused.ContainsKey(i)) step.Code = new global::app.goal.step.action.list.@this();   // open again
            else
            {
                // the step takes its code: with its warnings, and its defaults frozen
                foreach (var warning in warnings) step.Warning.Add(warning);
                foreach (var action in actions.Items()) action.Freeze(context);
            }
        }

        if (whole.Count == 0 && refused.Count == 0) return null;
        var problems = whole.Concat(refused.Values.SelectMany(p => p)).ToList();
        return new global::app.error.Error(string.Join("; ", problems), key ?? "StepsRefused", 400)
        {
            Details = new() { ["steps"] = string.Join(", ", refused.Keys) },
        };
    }

    /// <summary>The build's walk over the steps, in order, before the LLM answers: each step's code
    /// — or the code its certain picks know — walked over one scratch store, so each step is left the
    /// variables it reads with their known types (<c>step.Variable</c>). Nothing runs.</summary>
    public async System.Threading.Tasks.Task Scope(actor.context.@this context)
    {
        using var scratch = Scratch(context);
        for (int i = 0; i < CountRaw; i++) await this[i].Scope(scratch);
    }

    // The walk's own store: a System-actor context with no parent — a fresh variable store nothing
    // watches and nothing inherits into, so what the walk binds never reaches the builder's variables.
    private actor.context.@this Scratch(actor.context.@this context) => new(context.App, context.App.System);

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
