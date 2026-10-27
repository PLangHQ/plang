using app.data;
using System.Collections.Generic;
using System.Linq;

namespace app.goal.step.list;

/// <summary>
/// The step NODE — a goal's steps (<c>goal.Step</c>) or a control-flow action's branch body
/// (<c>action.Child</c>), a plang list value (<c>list&lt;step&gt;</c>) so a reader returns it directly. Its indexer is
/// positional, a step's place in this list; a step's own <c>Index</c> is its goal's numbering, which the goal answers
/// (<c>goal[index]</c>).
/// PROGRAM STRUCTURE: born context-free (the graph is shared across concurrent runs), it stores no
/// context — <see cref="Start"/> takes the ASK's. Owns its sequence-run (Rule 5 — the collection owns its
/// iteration). Twin of <see cref="app.goal.step.action.list.@this"/>.
/// </summary>
public sealed class @this : global::app.type.item.list.bare.@this<Step>
{
    // Two ways to be born: EMPTY (callers Add each step in — the reader, the parser, the fold), or
    // ADOPT a list value's rows (the value→slot materialization when a reader produced a generic list).
    public @this(global::app.type.@this type) : base(new List<object?>(), type) { }
    public @this(global::app.type.item.list.@this source, global::app.type.@this type) : base(source, type) { }

    // A view over steps another list holds: they are read through it, never taken into it.
    private @this(List<object?> steps, global::app.type.@this type) : base(steps, type) { }

    /// <summary>A step taken into this list is held by it, and says so (<see cref="global::app.goal.step.@this.List"/>):
    /// wherever it is added from (the reader, the parse, the build's fold), a step knows the list it goes on in.</summary>
    protected override void Admit(object? slot)
    {
        if ((slot is data.@this held ? held.Peek() : slot) is Step step) step.List = this;
    }

    protected override void Admit(global::app.type.item.list.@this other)
    {
        foreach (var slot in other.Slots()) Admit(slot);
    }

    /// <summary>Starts the steps in sequence. A return / exit propagates up (ShouldExit folds Returned).
    /// No indent skip-state — a fired control-flow action starts its own Child, so nesting is structural.
    /// Context is the ASK's, handed to each step's own Start. The node iterates ITSELF (the typed
    /// positional face).</summary>
    public async System.Threading.Tasks.Task<data.@this> Start(actor.context.@this context)
    {
        data.@this result = context.Ok();
        for (int i = 0; i < Count; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new global::app.error.@this("Operation was cancelled", "Cancelled", 499));
            result = await this[i].Start(context);
            if (result.ShouldExit()) break;
        }
        return result;
    }

    /// <summary>What follows <paramref name="step"/>, one of these, run in sequence from the one after it, as
    /// <see cref="Start"/> runs them (a step knows the list that holds it: <see cref="global::app.goal.step.@this.List"/>).
    /// Nothing follows a step this list doesn't hold.</summary>
    internal async System.Threading.Tasks.Task<data.@this> Rest(Step step, actor.context.@this context)
    {
        var position = -1;
        for (int i = 0; i < Count && position < 0; i++)
            if (ReferenceEquals(this[i], step)) position = i;
        data.@this result = context.Ok();
        if (position < 0) return result;
        for (int i = position + 1; i < Count; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new global::app.error.@this("Operation was cancelled", "Cancelled", 499));
            result = await this[i].Start(context);
            if (result.ShouldExit()) break;
        }
        return result;
    }

    /// <summary>Every step is cached — built before, word for word, its code standing (none reopened).
    /// A step list that is cached has nothing to ask.</summary>
    public bool IsCached => Items().All(s => s.IsCached);

    /// <summary>No step needs an answer — each is cached or written in formal: nothing to ask the
    /// decider or the LLM.</summary>
    public bool IsAnswered => Items().All(s => s.IsAnswered);

    /// <summary>The step list answers what it knows of itself (<c>%goal.Step.IsCached%</c>,
    /// <c>%goal.Step.IsAnswered%</c>); any other key is a list's read.</summary>
    public override async System.Threading.Tasks.ValueTask<data.@this> Get(data.@this parent, string key)
        => string.Equals(key, nameof(IsCached), System.StringComparison.OrdinalIgnoreCase) ? new data.@this(key, IsCached, parent: parent)
            : string.Equals(key, nameof(IsAnswered), System.StringComparison.OrdinalIgnoreCase) ? new data.@this(key, IsAnswered, parent: parent)
            : await base.Get(parent, key);

    /// <summary>The steps indented under the step at <paramref name="index"/> — the consecutive steps
    /// after it written deeper than it: its body as the author laid it out (the parser records each
    /// step's indent). Empty when nothing is indented under it. A view: the steps stay this list's.</summary>
    public @this Body(int index)
    {
        var steps = new List<object?>();
        if (index >= 0 && index < CountRaw)
        {
            var indent = this[index].Line.Indent;
            for (int j = index + 1; j < CountRaw && this[j].Line.Indent > indent; j++) steps.Add(this[j]);
        }
        return new @this(steps, Type);
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
    /// <para>A number the code writes that the words don't write as digits (<c>step.Unwritten</c>) is the
    /// decider's to confirm: when those numbers are the answer's only problems, the error is
    /// <c>UnwrittenNumber</c> with them under Details["numbers"], and their steps stay open; read again with the
    /// decider's <paramref name="confirmed"/> answers, a number it denies is refused as invented. Among other
    /// problems, an unconfirmed number is refused with them.</para>
    /// </summary>
    public async System.Threading.Tasks.Task<global::app.error.@this?> Read(string answer, actor.context.@this context,
        global::app.type.item.dict.@this? confirmed = null)
    {
        var whole = new List<string>();
        var heads = Head.Matches(answer);
        // an empty answer is whole: a step it leaves unanswered has no entry (one written in formal needs none)
        if (answer.Trim().Length > 0 && (heads.Count == 0 || answer[..heads[0].Index].Trim().Length > 0))
            whole.Add($"each step's line starts with its index: [0] action; action — the answer starts: {answer.Trim()[..System.Math.Min(120, answer.Trim().Length)]}");
        var lines = new Dictionary<int, string>();
        for (int n = 0; n < heads.Count; n++)
        {
            var i = int.Parse(heads[n].Groups[1].Value);
            var end = n + 1 < heads.Count ? heads[n + 1].Index : answer.Length;
            var line = answer[(heads[n].Index + heads[n].Length)..end].TrimEnd();
            if (i >= CountRaw) whole.Add($"entry {i} is extra: the goal has {CountRaw} steps");
            else if (!lines.TryAdd(i, line)) whole.Add($"step [{i}] is answered twice");
        }

        // each step's causes, whole: an error that refused it keeps its own key; a reason this read gives keys its check
        var refused = new SortedDictionary<int, List<global::app.error.@this>>();
        void Refuse(int i, global::app.error.@this cause) { if (!refused.TryGetValue(i, out var p)) refused[i] = p = new(); p.Add(cause); }
        // how a cause reads in the retry: under its step, with the reasons it holds
        string Said(int i, global::app.error.@this cause) => $"step {i} (\"{this[i].Text}\") — {cause.Message}"
            + (cause.list is { Count: > 0 } inner ? $": {string.Join("; ", inner.Select(c => c.FixSuggestion ?? c.Message))}" : "");
        string? key = null;
        // numbers the words don't write as digits, still to be confirmed by the decider
        var unconfirmed = new List<global::app.goal.step.unwritten.@this>();

        // Each open step's line, read.
        var read = new Dictionary<int, global::app.goal.step.action.list.@this>();
        // The modules every line is read against.
        var modules = context.App.module.list;
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
            if (line == null && !lines.TryGetValue(i, out line)) { Refuse(i, new global::app.error.@this("has no entry", "StepUnanswered", 400)); continue; }
            global::app.data.@this formal;
            // a value that refuses itself while it is read (a query that names no part) is this step's refusal; the
            // read goes on to the other steps
            try { formal = await new global::app.goal.step.action.formal.Reader(step, modules).Read(line, context); }
            catch (global::app.error.AppException refusal)
            {
                Refuse(i, new global::app.error.@this($"does not read, in: [{i}] {line.Trim()}", "StepUnread", 400) { list = [refusal.Error] });
                continue;
            }
            if (!formal.Success && step.IsFormal)
            {
                Refuse(i, new global::app.error.@this("is written in formal and does not read", "FormalUnread", 400) { list = [formal.Error!] });
                continue;
            }
            if (!formal.Success)
            {
                Refuse(i, new global::app.error.@this($"does not parse, in: [{i}] {line.Trim()}", "StepUnread", 400) { list = [formal.Error!] });
                var named = Named.Matches(line).Select(m => (m.Groups[1].Value, m.Groups[2].Value))
                    .Where(a => modules.Items().Any(m => string.Equals(m.Name, a.Item1, System.StringComparison.OrdinalIgnoreCase)
                        && m[a.Item2] != null))
                    .Select(a => $"{a.Item1}.{a.Item2}");
                foreach (var why in step.Pick.Unlisted(named)) Refuse(i, new global::app.error.@this(why, "ActionUnlisted", 400));
                continue;
            }
            var actions = (global::app.goal.step.action.list.@this)formal.Peek()!;
            foreach (var action in actions.Items()) await action.Reduce(context);
            read[i] = actions;
        }

        // Each read step, checked — every problem it shows, at once — in the goal's order, walked over
        // one scratch store: a step reads the variables the steps before it left.
        using var scratch = Scratch(context);
        // The steps are checked over the build's files, in a context born with them: a file a step saves is there
        // for a step after it that reads it — in this goal or a later one — and nothing reaches the disk. Its
        // variables are its own: the checks read none of the builder's (its goals, its answers), and the settings
        // come through the parent.
        using var check = new actor.context.@this(context.App, context.Actor, parent: context,
            filesystem: context.App.Build?.Files ?? throw new System.InvalidOperationException("A goal's steps are read in a build: the app has no Build."));
        // The steps are checked inside their goal's frame, as they will run: what a step names relative to its
        // goal (a file 'note.txt') is found where the goal is, not where the builder's own goal is. Each context
        // has its own calls, so the frame is pushed in each the checks run in.
        if (CountRaw > 0) await Steps(this[0].Goal);

        // The steps checked inside their goal's frame, in each context the checks run in — pushed here, where the
        // checks run, so each frame is current for them and ends with them.
        async System.Threading.Tasks.Task Steps(global::app.goal.@this goal)
        {
            await using (context.call.Push(goal, await context.call.Setting()))
            await using (check.call.Push(goal, await check.call.Setting()))
            await using (scratch.call.Push(goal, await scratch.call.Setting()))
                for (int i = 0; i < CountRaw; i++)
                {
                    var step = this[i];
                    if (!read.TryGetValue(i, out var actions)) { await step.Scope(scratch); continue; }
                    try { await Check(i, step, actions); }
                    catch (global::app.error.AppException refusal)
                    {
                        // a value that refuses itself while its step is checked is this step's refusal; the step opens
                        // again and the check goes on to the other steps
                        Refuse(i, new global::app.error.@this($"does not read, in: [{i}] {step.Text}", "StepUnread", 400) { list = [refusal.Error] });
                        step.Code = new global::app.goal.step.action.list.@this(step.Code.Type);
                    }
                }
        }

        // One read step checked: its body, its code's own verdict, what its words hold, its numbers, its build, its
        // picks and its scope — each problem refused under its index; the step takes its code or opens again.
        async System.Threading.Tasks.Task Check(int i, global::app.goal.step.@this step, global::app.goal.step.action.list.@this actions)
        {
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
                    if (inside.All(spare.Remove)) action.Child = new @this(action.Child.Type);
                    else Refuse(i, new global::app.error.@this($"step {i}'s body is the steps indented under it ({string.Join(", ", body.Items().Select(b => b.Index))}); " +
                                   $"the builder places them — write step {i} without {{ }} and each indented step on its own line", "BodyIndented", 400));
                }
            }
            step.Code = actions;
            var invalid = await step.Validate(check);
            if (invalid != null)
            {
                key ??= invalid.Key == "ElseWithoutIf" ? invalid.Key : null;
                foreach (var cause in invalid.list) Refuse(i, cause);
            }
            // the answer holds what the step's words say — checked on the answer as written, before the
            // handlers' Build may change it
            foreach (var uncovered in await step.Cover(check)) Refuse(i, new global::app.error.@this(uncovered, "NotCovered", 400));
            // a number the words don't write as digits: confirmed by the decider, or asked
            var asked = false;
            foreach (var number in await step.Unwritten(check, step.IsFormal ? step.Text : lines.GetValueOrDefault(i)))
            {
                if (confirmed == null) { unconfirmed.Add(number); asked = true; }
                else if (!await number.Given(confirmed, check)) Refuse(i, new global::app.error.@this(number.Message, "UnwrittenNumber", 400));
            }
            // only code that judged itself sound is built
            if (invalid == null && await actions.Build(check) is { } failed)
                foreach (var cause in failed.list) Refuse(i, cause);
            // a step written in formal was asked of no decider: there are no picks to agree with
            var (disagree, warnings) = step.IsFormal
                ? (new List<string>(), new List<global::app.warning.@this>()) : await step.Pick.Agree(actions, context);
            foreach (var why in disagree) Refuse(i, new global::app.error.@this(why, "PickDisagrees", 400));
            foreach (var declined in await step.Scope(scratch)) Refuse(i, declined);
            if (refused.ContainsKey(i) || asked) step.Code = new global::app.goal.step.action.list.@this(step.Code.Type);   // open again
            else
            {
                // the step takes its code: with its warnings, and its defaults frozen
                foreach (var warning in warnings) step.Warning.Add(warning);
                foreach (var action in actions.Items()) action.Freeze(context);
            }
        }

        // each step now says why it is refused, or that it is not: a step that stays refused after the retry is
        // written into the .pr broken, with its reasons, and its goal does not start. A step that built (or kept its
        // code from an earlier read) holds none.
        // It names the step; the reasons are its causes, each said once.
        for (int i = 0; i < CountRaw; i++)
            this[i].Error = refused.TryGetValue(i, out var why)
                ? new global::app.error.@this($"step {i} (\"{this[i].Text}\") is broken", "StepBroken", 400)
                    { list = why }
                : null;

        if (whole.Count == 0 && refused.Count == 0 && unconfirmed.Count == 0) return null;
        // numbers to confirm are asked of the decider first (the builder's ConfirmNumbers), whatever else is wrong: a
        // number it confirms is no problem, so nothing is refused before it answers — the confirmed read refuses only
        // what is really wrong, and a step whose number is confirmed keeps its answer
        if (unconfirmed.Count > 0)
            return new global::app.error.@this(
                $"{unconfirmed.Count} number(s) the steps don't write as digits: {string.Join("; ", unconfirmed.Select(n => $"step {n.Step} {n.Action}.{n.Property}={n.Value}"))}",
                "UnwrittenNumber", 400)
            {
                Details = new()
                {
                    ["numbers"] = unconfirmed,
                    ["steps"] = string.Join(", ", unconfirmed.Select(n => n.Step).Distinct()),
                },
            };
        var problems = whole.Concat(refused.SelectMany(p => p.Value.Select(cause => Said(p.Key, cause)))).ToList();
        return new global::app.error.@this(string.Join("; ", problems), key ?? "StepsRefused", 400)
        {
            // the goal whose steps these are: a build that ends on it names the goal by its path
            Goal = CountRaw > 0 ? this[0].Goal : null,
            Details = new() { ["steps"] = string.Join(", ", refused.Keys) },
        };
    }

    /// <summary>The build's walk over the steps, in order, before the LLM answers: each step's code
    /// — or the code its certain picks know — walked over one scratch store, so each step is left the
    /// variables it reads with their known types (<c>step.Typed</c>). Nothing runs.</summary>
    public async System.Threading.Tasks.Task Scope(actor.context.@this context)
    {
        using var scratch = Scratch(context);
        for (int i = 0; i < CountRaw; i++) await this[i].Scope(scratch);
    }

    // The walk's own store: a System-actor context with no parent — a fresh variable store nothing
    // watches and nothing inherits into, so what the walk binds never reaches the builder's variables.
    private actor.context.@this Scratch(actor.context.@this context) => new(context.App, context.App.actor.list.System);
}
