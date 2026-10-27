using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using app.Attributes;
using app.@event;
using app.module;
using app.Utils;
namespace app.goal;

/// <summary>
/// Visibility of a goal.
/// </summary>
[global::app.Attributes.PlangType("visibility"), global::app.Attributes.Choice]
public enum Visibility
{
    Private = 0,
    Public = 1
}

/// <summary>
/// Represents a goal (a .goal file or sub-goal) for App.
/// </summary>
public sealed partial class @this
{

    // A goal is an item (this.Item.cs): plang carries it as itself, written and read through its
    // [Store]/[Out] props.

    // Read-time scalars are `internal set`, not `init`: the reader constructs the goal SHELL first
    // (so its steps and sub-goals can be born holding it) and fills these as they arrive off the
    // stream. Immutable to everything outside the assembly.
    [Store, Out, LlmBuilder, Debug, Default, Snapshot]
    public string Name { get; internal set; } = "";

    /// <summary>The goal's description: the comment lines written above its name.</summary>
    [Store, Out, LlmBuilder, Debug, Default]
    public string? Comment { get; internal set; }

    /// <summary>A goal, born holding its type and its step, child, tag and property lists from <paramref name="types"/>, the
    /// app's type list (read at birth, never kept).</summary>
    public @this(global::app.type.list.@this types) : base(types[typeof(@this)])
    {
        _step = new(types[typeof(global::app.goal.step.list.@this)]);
        Child = new(this, types[typeof(child.@this)]);
        Tag = new(types[typeof(global::app.goal.tag.list.@this)]);
        Warning = new(types);
        property = new(types[typeof(global::app.type.item.property.list.@this)]);
    }

    private global::app.goal.step.list.@this _step;
    [Store, Out, LlmBuilder, Debug, Default]
    public global::app.goal.step.list.@this Step
    {
        // A plain slot. Every step in it was born knowing this goal — the reader constructs the
        // goal shell first and hands it down, so there is nothing to repair on read.
        get => _step;
        set => _step = value;
    }

    /// <summary>The step this goal numbered <paramref name="index"/> — its own, or one in a body under them (the goal
    /// numbers its steps in written order, a body's numbered on from the step that holds it); null when none is. Its
    /// <see cref="Step"/> list's indexer is positional, the place in that list.</summary>
    public global::app.goal.step.@this? this[int index] => Numbered(_step, index);

    // The step numbered index in steps, or in a body under one of them.
    private global::app.goal.step.@this? Numbered(global::app.goal.step.list.@this steps, int index)
    {
        foreach (var step in steps.Items())
        {
            if (step.Index == index) return step;
            foreach (var action in step.Code.Items())
                if (Numbered(action.Child, index) is { } held) return held;
        }
        return null;
    }

    /// <summary>The goals written after this one in its file; each knows this goal as its parent.</summary>
    [Store, Out, LlmBuilder, Debug, Default]
    public child.@this Child { get; }

    /// <summary>The goal of <paramref name="name"/> in this goal's file, from here down: this goal, else one of its
    /// children at any depth; null when none is.</summary>
    internal @this? Named(string name)
    {
        if (string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)) return this;
        foreach (var child in Child.Items())
            if (child.Named(name) is { } named) return named;
        return null;
    }

    /// <summary>A file's first goal is public; the goals under it are private to it.</summary>
    [Store, Out, LlmBuilder, Debug, Default]
    public global::app.type.item.choice.@this<Visibility> Visibility
        => Parent == null ? global::app.goal.Visibility.Public : global::app.goal.Visibility.Private;

    /// <summary>The name of the goal's .goal file: where its .pr is found is where it is. A sub-goal is in its
    /// parent's file and names none.</summary>
    [Store, Out]
    public string? File => Parent == null ? Path?.Name.ToString() : null;

    public override string ToString()
    {
        var sb = new StringBuilder(Name);
        foreach (var step in Step.Items())
        {
            // a line ends at LF on every OS, as a .goal's and a stream's do
            sb.Append('\n');
            sb.Append(new string(' ', step.Line.Indent * 4));
            sb.Append("- ");
            sb.Append(step.Text);
        }
        return sb.ToString();
    }

    /// <summary>Where it runs, when that is another app's (<c>%!app.parent.goal["Claude"]%</c>): its run goes
    /// there with the call's arguments, and what comes back is its result. Null for a goal of this app — its steps run.</summary>
    internal global::app.parent.@this? Elsewhere { get; init; }

    /// <summary>The goal's .goal file — where its .pr is found. A sub-goal is in its parent's file. In a snapshot it is
    /// part of what finds the goal: the goal list loads it from there.</summary>
    [Debug, LlmBuilder, Snapshot]
    public global::app.type.item.path.@this? Path
    {
        get => Parent?.Path ?? _path;
        set
        {
            _path = value;
            // the .pr it is built to, worked out where its file is given: a file reference, of the types this goal's own
            // type is among
            _pr = value is global::app.type.item.path.file.@this { Absolute.Length: > 0 } source && Type.Types is { } types
                ? new global::app.type.item.file.@this(source.Pr, types) : null;
        }
    }
    private global::app.type.item.path.@this? _path;
    private global::app.type.item.file.@this? _pr;

    /// <summary>The folder holding this goal's source .goal file — what a relative path in it resolves against.
    /// Null for a goal with no Path.</summary>
    public global::app.type.item.path.@this? Folder => Path?.Parent;

    /// <summary>The .pr this goal is built to, a file reference (its file's <c>Pr</c>), worked out where its Path is set; a
    /// sub-goal's is its parent's; none without a Path.</summary>
    [Debug]
    public global::app.type.item.file.@this? Pr => Parent?.Pr ?? _pr;

    [Store, Out, Debug, Snapshot]
    public string? Hash
    {
        get
        {
            if (_hash != null) return _hash;
            if (Step.Count == 0) return null;

            // the source as written, comments included: the goal's description and name, then every
            // step with its comment, in the order written: a step indented under a condition is folded
            // into it (its body) and still counts; a condition's inline { } body is written on its
            // parent's line and is not a step of the source
            var sb = new StringBuilder();
            sb.Append(Comment).Append('\n').Append(Name);
            void Append(global::app.goal.step.@this step)
            {
                sb.Append('\n').Append(step.Comment).Append('\n').Append(step.Text);
                foreach (var action in step.Code.Items())
                    foreach (var body in action.Child.Items())
                        if (body.Line.Number != step.Line.Number) Append(body);
            }
            foreach (var step in Step.Items()) Append(step);

            _hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
            return _hash;
        }
        internal set => _hash = value;
    }
    private string? _hash;

    [Store, Out, Debug]
    public string? BuilderVersion { get; set; }

    /// <summary>A setup goal — named Setup, or in the app's <c>/setup/</c>: it runs only through setup.</summary>
    [Debug]
    public bool IsSetup => Name.Equals("Setup", StringComparison.OrdinalIgnoreCase) || Under("setup");

    /// <summary>A system goal — under <c>/system/</c>: the app's own copy or the os's, wherever it was loaded from
    /// (an os goal's plang path is <c>/system/…</c>).</summary>
    [Debug]
    public bool IsSystem => Under("system");

    /// <summary>A test goal — its file is a <c>.test.goal</c>.</summary>
    [Debug]
    public bool IsTest => Where.EndsWith(".test.goal", StringComparison.OrdinalIgnoreCase);

    // Where the goal lives, in its plang form without the leading slash — what the three facts above read, with nothing
    // made (a step's events ask IsSystem each time it starts).
    private ReadOnlySpan<char> Where => (Path?.Raw ?? "").AsSpan().TrimStart("/\\");

    // Whether it lives in the app's top folder named so, either separator after it.
    private bool Under(string folder)
        => Where.StartsWith(folder, StringComparison.OrdinalIgnoreCase) && Where.Length > folder.Length
           && Where[folder.Length] is '/' or '\\';

    /// <summary>The goal's tags — a build-birth fact stamped by <c>test.tag</c>'s Build hook (skip,
    /// user tags). A <c>list&lt;tag&gt;</c> program node; each tag owns its case-insensitive equality.</summary>
    [Store, Out, Debug]
    public global::app.goal.tag.list.@this Tag { get; init; }


    [LlmIgnore]
    [JsonIgnore]
    public @this? Parent { get; set; }

    [Debug]
    [LlmBuilder]
    public global::app.warning.list.@this Warning { get; init; }

    /// <summary>The goal's app-absolute address — the name a call anywhere in the app reaches it by:
    /// its .goal path without the extension (<c>/system/builder/EmitBuildEvent</c>); a sub-goal is its
    /// parent's address and its name (<c>/start#show</c>). Null for a goal with no Path.</summary>
    [JsonIgnore]
    public string? Address
    {
        get
        {
            if (Parent is { } parent) return parent.Address is { } above ? $"{above}#{Name}" : null;
            var path = Path?.ToString();
            if (string.IsNullOrEmpty(path)) return null;
            return path.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? path[..^5] : path;
        }
    }

    /// <summary>The name a call from <paramref name="caller"/> writes for this goal — its <see cref="Address"/>,
    /// or null when the call keeps the name it was written with: a goal in the caller's own file (a child, or
    /// the file's root) stays bare, it wins by rule and can't be shadowed.</summary>
    public string? Reference(@this? caller) => Equals(Path, caller?.Path) ? null : Address;

    /// <summary>
    /// Merges LLM-derived fields from an existing built goal onto this freshly-parsed goal.
    /// Delegates to Steps.Merge for this goal's steps, then recurses into sub-goals
    /// matched by Name so every sub-goal's steps also get their prior Actions + PriorText
    /// back. Without the recursion, @known / keep:true only fire for the top-level goal
    /// of a multi-goal .goal file.
    /// </summary>
    public void Merge(@this? existing)
    {
        if (existing == null) return;

        Cache = existing;
        // the source's hash — every step as written, in order — read before any step takes back its
        // code: a condition's code carries its body, which the source already lists
        _ = Hash;

        // Exact-text match only — robust to reorder/insert/delete; a text change drops the prior
        // mapping and the LLM rebuilds that step fresh. Sets PriorText so the builder can emit @known.
        // The prior steps are every step as the .pr holds it, a condition's folded body included (the
        // source lists an indented body as steps of its own, as the hash walks them).
        var prior = new List<global::app.goal.step.@this>();
        void Collect(global::app.goal.step.@this step)
        {
            prior.Add(step);
            foreach (var action in step.Code.Items())
                foreach (var body in action.Child.Items())
                    if (body.Line.Number != step.Line.Number) Collect(body);
        }
        foreach (var step in existing.Step.Items()) Collect(step);
        if (prior.Count > 0)
        {
            var consumed = new HashSet<int>();
            foreach (var step in Step.Items())
                for (int i = 0; i < prior.Count; i++)
                {
                    if (consumed.Contains(i)) continue;
                    if (prior[i].Text == step.Text)
                    {
                        step.Merge(prior[i]);
                        step.PriorText = prior[i].Text;
                        consumed.Add(i);
                        break;
                    }
                }
        }

        if (existing.Child.CountRaw == 0) return;
        foreach (var subGoal in Child.Items())
        {
            var priorSub = existing.Child.Items().FirstOrDefault(g =>
                string.Equals(g.Name, subGoal.Name, StringComparison.OrdinalIgnoreCase));
            if (priorSub != null) subGoal.Merge(priorSub);
        }

    }

    /// <summary>The goal as its .pr holds it — the last build of this source (goal.Merge). Null when
    /// the goal has no .pr yet.</summary>
    [JsonIgnore]
    [global::app.LlmBuilder]
    public @this? Cache { get; private set; }

    /// <summary>The goal is cached: its source is the one its <see cref="Cache"/> was built from (every
    /// step as written, in order, and the same sub-goals), every step is cached (none reopened), and so
    /// is every sub-goal. A cached goal isn't built again — its cache stands.</summary>
    [JsonIgnore, LlmBuilder]
    public bool IsCached => Cache != null && Cache.Hash == Hash && Cache.Child.CountRaw == Child.CountRaw
        && Step.IsCached && Child.Items().All(g => g.IsCached);

    /// <summary>The first step whose saved code no longer holds against today's catalogue — this goal's, then its
    /// sub-goals' — or null when all hold: whether a built goal is still the one its words would build.</summary>
    internal async Task<global::app.error.@this?> Invalid(actor.context.@this context)
    {
        foreach (var step in Step.Items())
            if (await step.Validate(context) is { } invalid) return invalid;
        foreach (var subGoal in Child.Items())
            if (await subGoal.Invalid(context) is { } invalid) return invalid;
        return null;
    }

    /// <summary>A cached step whose saved code no longer holds against today's catalogue (an action gone
    /// or renamed, a check it now fails) is opened again — its code cleared — and rebuilt, with a
    /// warning saying why. The step's own judgement decides; sub-goals are judged the same way.</summary>
    public async Task Reopen(actor.context.@this context)
    {
        foreach (var step in Step.Items().Where(s => s.IsCached).ToList())
        {
            if (await step.Validate(context) is not { } invalid) continue;
            step.Code = new global::app.goal.step.action.list.@this(step.Code.Type);
            step.Warning.Add(new global::app.warning.@this(context.App.type.list)
            {
                Key = "Reopened",
                Message = $"step {step.Index}'s saved code no longer holds — rebuilt: {invalid.Message}",
            });
        }
        foreach (var subGoal in Child.Items()) await subGoal.Reopen(context);
    }

    /// <summary>
    /// The goal <paramref name="source"/> (a <c>.goal</c>) is built to, loaded as <paramref name="app"/> itself from
    /// its .pr (<see cref="PrPath"/>): the one already held from there, else read through its <c>on.load</c> — what is bound
    /// before a goal loads is the goal type's, handed the .pr (there is no goal yet), and a failure or a Handled
    /// answer is the load's answer with nothing read; the path reads itself into a goal, the app's goals hold it,
    /// and what is bound after it runs across the goal's levels, handed the goal. A setup goal is refused — it runs
    /// only through setup.
    /// </summary>
    // A goal by its location, as item's ILoad door asks it: the app's goals load it.
    public static Task<data.@this> Load(global::app.type.item.path.@this source, global::app.@this app) => app.goal.list.Load(source);

    /// <summary>What the goal knows of the .pr it was read from; a goal not read from a file has none.</summary>
    internal stamp Stamp { get; set; } = new stamp.none();

    /// <summary>A goal's .pr as the goal was read from it — each kind says whether the file is still as it was.</summary>
    internal abstract record stamp
    {
        internal abstract Task<bool> IsFresh(actor.context.@this context);

        /// <summary>Not read from a file: the goal is as it is.</summary>
        internal sealed record none : stamp
        {
            internal override Task<bool> IsFresh(actor.context.@this context) => Task.FromResult(true);
        }

        /// <summary>Read from a file whose stat failed: what it held is unknown, so it is read again.</summary>
        internal sealed record unknown : stamp
        {
            internal override Task<bool> IsFresh(actor.context.@this context) => Task.FromResult(false);
        }

        /// <summary>Read from <paramref name="Pr"/> with <paramref name="Stat"/>: still as it was while its stat is the same.</summary>
        internal sealed record stat(global::app.type.item.path.@this Pr, global::app.type.item.path.@this.StatInfo Stat) : stamp
        {
            internal override async Task<bool> IsFresh(actor.context.@this context)
                => await Pr.Stat(context) is { Success: true } now && await now.Value() is { } info && info.Same(Stat);
        }
    }

    /// <summary>Whether the .pr this goal was read from is as it was then.</summary>
    internal Task<bool> IsFresh(actor.context.@this context) => Stamp.IsFresh(context);

    /// <summary>A goal starts through the goal type's events, then its own. A goal of plang's own (<see cref="IsSystem"/>)
    /// is none of the app's goals, so it starts through its own events alone: "each goal" never hears it, a binding on
    /// it by name does.</summary>
    protected internal override global::app.type.item.@this? Level(int depth, actor.context.@this context) => depth switch
    {
        0 => IsSystem ? this : context.App.goal.type,
        1 when !IsSystem => this,
        _ => null,
    };

    /// <summary>
    /// Starts this goal through its <c>on.start</c>: what is bound before it, then its steps, then what is bound
    /// after it (<see cref="Level"/>). A before that fails or cancels is the result: the steps don't start, every
    /// after still runs on it. Context travels as parameter — goals may be cached/shared.
    /// </summary>
    public Task<data.@this> Start(actor.context.@this context) => Start(context, null);

    /// <summary>Starts this goal with its frame born with <paramref name="parameters"/> — a goal call's: they are this
    /// run's variables, for as long as it runs.</summary>
    public async Task<data.@this> Start(actor.context.@this context, IEnumerable<data.@this>? parameters)
    {
        if (context.CancellationToken.IsCancellationRequested)
            return context.Error(new global::app.error.@this("Operation was cancelled", "Cancelled", 499));

        // A goal holding a step the build could not fix does not start: it answers why, before its frame and before
        // anything bound to its start, so nothing of it ran.
        if (Broken() is { } broken) return context.Error(broken);

        // The goal's own frame spans its whole run — what is bound before and after it, and its steps — so the
        // goal in play is this one throughout. The frame outlives any single step, so things like `debug.tag`
        // can attach metadata to a scope later steps still read (up via Current.Caller). A goal may call
        // itself: the only limit is the depth guard, which trips at this Push.
        global::app.call.@this frame;
        var setting = await context.call.Setting();
        try { frame = context.call.Push(this, setting, parameters); }
        catch (global::app.error.CallStackOverflowException ex)
        {
            return context.Error(context.call.Overflow(ex, this, null));
        }
        await using var _frame = frame;

        var answer = await on.start.Before(this, context);
        var result = answer is { Success: false } or { Handled: true } ? answer : await Enter(context);
        return await on.start.After(this, result, context);
    }

    // Why this goal cannot start: it holds steps the build could not fix. Its own message names the goal's path and
    // the steps' indexes; each broken step's error rides whole as a cause — its text and its reasons are there.
    // Null when every step built.
    private global::app.error.@this? Broken()
    {
        var steps = Step.Items().Where(s => s.Error != null).ToList();
        if (steps.Count == 0) return null;
        return new global::app.error.@this(
            $"goal '{Path}' has {steps.Count} broken step(s): {string.Join(", ", steps.Select(s => s.Index))}",
            "GoalBroken", 400)
        { Goal = this, list = steps.Select(s => s.Error!).ToList(), FixSuggestion = "fix the words and build again" };
    }

    // Starts the steps, and folds a return that ends here — or, for a goal of another app, runs it there.
    private async Task<data.@this> Enter(actor.context.@this context)
    {
        if (Elsewhere is { } app) return await app.Run(Name, context);
        var result = await Step.Start(context);
        if (result.Returned)
        {
            result.ReturnDepth--;
            if (result.ReturnDepth <= 0)
                result.Returned = false;
        }
        return result;
    }

    /// <summary>
    /// Parses .goal file text into a list of Child.
    /// The first goal holds the Path; the rest are its sub-goals, in its file. First goal is Public, rest are Private.
    ///
    /// Stays <c>static</c>: relocating this onto <c>path</c>
    /// (<c>path.ReadAsGoal</c>) or <c>text</c> (<c>text.AsGoal</c>) would put a
    /// Goal-specific method on a general-purpose value type — too specific for
    /// that general object. A source-text → Goal factory is the sanctioned
    /// exception to the no-static rule.
    /// <para><paramref name="context"/> is the caller's: setup/system/test placement is read from
    /// the path's root-relative form under the caller's root, never from its display text.</para>
    /// </summary>
    public static @this? Parse(string text, global::app.type.item.path.@this? path, global::app.actor.context.@this context)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Replace("\t", "    ");

        var lines = text.Split('\n');
        var goals = new List<@this>();
        var goal = (@this?)null;
        // The step being read: continuation lines rebuild it, and it is added to its goal's steps at each boundary
        // (next step / blank / goal / EOF).
        var step = (Step?)null;
        // the comment lines read since the last step or goal, the comment of whichever comes next
        var comment = new StringBuilder();
        var inBlockComment = false;
        var stepIndex = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var raw = lines[i].TrimEnd('\r');

            // Block comment continuation
            if (inBlockComment)
            {
                var endIdx = raw.IndexOf("*/");
                if (endIdx >= 0)
                {
                    var closePart = raw[..endIdx].Trim();
                    if (closePart.Length > 0)
                    {
                        if (comment.Length > 0) comment.Append('\n');
                        comment.Append(closePart);
                    }
                    inBlockComment = false;
                }
                else
                {
                    if (comment.Length > 0) comment.Append('\n');
                    comment.Append(raw.Trim());
                }
                continue;
            }

            var trimmed = raw.TrimStart();

            // Block comment start
            if (trimmed.StartsWith("/*"))
            {
                var afterOpen = trimmed[2..];
                var closeIdx = afterOpen.IndexOf("*/");
                if (closeIdx >= 0)
                {
                    if (comment.Length > 0) comment.Append('\n');
                    comment.Append(afterOpen[..closeIdx].Trim());
                }
                else
                {
                    inBlockComment = true;
                    if (comment.Length > 0) comment.Append('\n');
                    comment.Append(afterOpen.Trim());
                }
                continue;
            }

            // Blank line — comment boundary
            if (string.IsNullOrWhiteSpace(raw))
            {
                comment.Clear();
                if (step != null) { goal!.Step.Add(step); step = null; }
                continue;
            }

            // Line comment — any line starting with /
            if (trimmed.StartsWith("/"))
            {
                if (comment.Length > 0) comment.Append('\n');
                comment.Append(trimmed[1..].TrimStart());
                continue;
            }

            // Step line
            if (trimmed.StartsWith("- ") || trimmed == "-")
            {
                if (goal == null)
                {
                    goal = new @this(context.App.type.list)
                    {
                        Name = "Start",
                        Path = path
                    };
                    goals.Add(goal);
                    stepIndex = 0;
                }

                // Finalize the previous step before starting this one (continuations are done).
                if (step != null) goal.Step.Add(step);

                // The step's text is canonical at birth: every line trimmed at both ends (its \r gone),
                // continuation lines joined by \n — nothing inside a line changes. Readers compare and
                // print it as is (goal.Merge's `==`, the prompts), never trimming it again.
                step = new Step(context.App.type.list)
                {
                    Goal = goal,   // birth fact — the parser holds the goal, so it hands it over
                    List = goal.Step,
                    Index = stepIndex,
                    Text = trimmed.Length > 2 ? trimmed[2..].Trim() : "",
                    Line = new global::app.goal.step.line.@this { Number = lineNumber, Indent = (raw.Length - trimmed.Length) / 4 },
                    Comment = comment.Length > 0 ? comment.ToString() : null
                };
                comment.Clear();

                stepIndex++;
                continue;
            }

            // Continuation line
            if (step != null && raw.Length > 0 && raw[0] == ' ')
            {
                step = new Step(context.App.type.list)
                {
                    Goal = step.Goal,
                    List = step.List,
                    Index = step.Index,
                    Text = step.Text + "\n" + trimmed.TrimEnd(),
                    Line = step.Line,
                    Comment = step.Comment
                };
                continue;
            }

            // Escape character — \ at start of line continues previous step text, the backslash dropped
            if (step != null && trimmed.StartsWith("\\"))
            {
                step = new Step(context.App.type.list)
                {
                    Goal = step.Goal,
                    List = step.List,
                    Index = step.Index,
                    Text = step.Text + "\n" + trimmed[1..].TrimEnd(),
                    Line = step.Line,
                    Comment = step.Comment
                };
                continue;
            }

            // Goal header. Finalize the previous goal's last step before starting the new goal.
            if (step != null) { goal?.Step.Add(step); step = null; }

            // what the goal is — setup, system, a test — it answers itself from where it lives; a sub-goal lives in
            // its parent's file, so only the file's first goal holds the path
            goal = new @this(context.App.type.list)
            {
                Name = trimmed,
                Comment = comment.Length > 0 ? comment.ToString() : null,
                Path = goals.Count == 0 ? path : null,
            };
            comment.Clear();
            goals.Add(goal);
            stepIndex = 0;
        }

        // Finalize the last step at EOF.
        if (step != null) goal?.Step.Add(step);

        // Sub-goals nest under the root (public) goal
        if (goals.Count > 1)
        {
            for (int i = 1; i < goals.Count; i++)
                goals[0].Child.Add(goals[i]);
        }

        return goals[0];
    }

}
