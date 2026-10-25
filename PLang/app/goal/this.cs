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
    [Store, Out, LlmBuilder, Debug, Default]
    public string Name { get; internal set; } = "";

    /// <summary>The goal's description: the comment lines written above its name.</summary>
    [Store, Out, LlmBuilder, Debug, Default]
    public string? Comment { get; internal set; }

    /// <summary>A goal, born holding its type and its step, child and tag lists from <paramref name="types"/>, the
    /// app's type list (read at birth, never kept).</summary>
    public @this(global::app.type.list.@this types) : base(types[typeof(@this)])
    {
        _step = new(types[typeof(global::app.goal.step.list.@this)]);
        Child = new(this, types[typeof(child.@this)]);
        Tag = new(types[typeof(global::app.goal.tag.list.@this)]);
        Warning = new(types);
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

    /// <summary>The goals written after this one in its file; each knows this goal as its parent.</summary>
    [Store, Out, LlmBuilder, Debug, Default]
    public child.@this Child { get; }

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
            sb.AppendLine();
            sb.Append(new string(' ', step.Line.Indent * 4));
            sb.Append("- ");
            sb.Append(step.Text);
        }
        return sb.ToString();
    }

    /// <summary>Where it runs, when that is another app's (<c>%!app.parent.goal["Claude"]%</c>): its run goes
    /// there with the call's arguments, and what comes back is its result. Null for a goal of this app — its steps run.</summary>
    internal global::app.parent.@this? Elsewhere { get; init; }

    /// <summary>The goal's .goal file — where its .pr is found. A sub-goal is in its parent's file.</summary>
    [Debug, LlmBuilder]
    public global::app.type.item.path.@this? Path
    {
        get => Parent?.Path ?? _path;
        set
        {
            _path = value;
            // the .pr it is built to, worked out where its file is given
            _pr = value is global::app.type.item.path.file.@this { Absolute.Length: > 0 } source ? source.Pr : null;
        }
    }
    private global::app.type.item.path.@this? _path;
    private global::app.type.item.path.@this? _pr;

    /// <summary>The folder holding this goal's source .goal file — what a relative path in it resolves against.
    /// Null for a goal with no Path.</summary>
    public global::app.type.item.path.@this? Folder => Path?.Parent;

    /// <summary>The .pr this goal is built to (<see cref="Pr"/>), worked out where its Path is set; a sub-goal's is its
    /// parent's; none without a Path.</summary>
    [Debug]
    public global::app.type.item.path.@this? PrPath => Parent?.PrPath ?? _pr;

    [Store, Out, Debug]
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
            void Written(global::app.goal.step.@this step)
            {
                sb.Append('\n').Append(step.Comment).Append('\n').Append(step.Text);
                foreach (var action in step.Code.Items())
                    foreach (var body in action.Child.Items())
                        if (body.Line.Number != step.Line.Number) Written(body);
            }
            foreach (var step in Step.Items()) Written(step);

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
    public bool IsSetup => Name.Equals("Setup", StringComparison.OrdinalIgnoreCase)
                           || Where.StartsWith("setup/", StringComparison.OrdinalIgnoreCase);

    /// <summary>A system goal — under <c>/system/</c>: the app's own copy or the os's, wherever it was loaded from
    /// (an os goal's plang path is <c>/system/…</c>).</summary>
    [Debug]
    public bool IsSystem => Where.StartsWith("system/", StringComparison.OrdinalIgnoreCase);

    /// <summary>A test goal — its file is a <c>.test.goal</c>.</summary>
    [Debug]
    public bool IsTest => Where.EndsWith(".test.goal", StringComparison.OrdinalIgnoreCase);

    // Where the goal lives, in its plang form without the leading slash — what the three facts above read.
    private string Where => (Path?.Raw ?? "").Replace('\\', '/').TrimStart('/');

    /// <summary>The goal's tags — a build-birth fact stamped by <c>test.tag</c>'s Build hook (skip,
    /// user tags). A <c>list&lt;tag&gt;</c> program node; each tag owns its case-insensitive equality.</summary>
    [Store, Out, Debug]
    public global::app.goal.tag.list.@this Tag { get; init; }


    /// <summary>
    /// Folder path of this goal, derived from Path.
    /// Starts with / (relative to app root, not OS root).
    /// E.g., \Cache\Start.goal → /Cache/, \Start.goal → /
    /// </summary>
    [JsonIgnore]
    [LlmIgnore]
    public global::app.type.item.path.@this? FolderPath
    {
        get => Path?.Parent;
    }

    [LlmIgnore]
    [JsonIgnore]
    public @this? Parent { get; set; }

    [Debug]
    [LlmBuilder]
    public global::app.warning.list.@this Warning { get; init; }

    [Debug, Default]
    public string FullPath
    {
        get
        {
            if (Parent == null)
                return Name;
            return $"{Parent.FullPath}/{Name}";
        }
    }

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

    public string ToText()
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(Comment))
            lines.Add($"/ {Comment}");

        lines.Add(Name);

        foreach (var step in Step.Items())
        {
            if (!string.IsNullOrEmpty(step.Comment))
                lines.Add($"/ {step.Comment}");

            var prefix = new string(' ', step.Line.Indent) + "- ";
            lines.Add(prefix + step.Text);
        }

        return string.Join("\n", lines);
    }

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
        void Held(global::app.goal.step.@this step)
        {
            prior.Add(step);
            foreach (var action in step.Code.Items())
                foreach (var body in action.Child.Items())
                    if (body.Line.Number != step.Line.Number) Held(body);
        }
        foreach (var step in existing.Step.Items()) Held(step);
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
    /// its <see cref="Pr"/>: the one already held from there, else read through its <c>on.load</c> — what is bound
    /// before a goal loads is the goal type's, handed the .pr (there is no goal yet), and a failure or a Handled
    /// answer is the load's answer with nothing read; the path reads itself into a goal, the app's goals hold it,
    /// and what is bound after it runs across the goal's levels, handed the goal. A setup goal is refused — it runs
    /// only through setup.
    /// </summary>
    // A goal by its location, as item's ILoad door asks it: the app's goals load it.
    public static Task<data.@this> Load(global::app.type.item.path.@this source, global::app.@this app) => app.goal.list.Load(source);

    /// <summary>What the goal knows of the .pr it was read from: the file's stat then — none when the stat failed, so
    /// it is read again. Null for a goal not read from a file, which is as it is.</summary>
    internal Stamp? Read { get; set; }

    /// <summary>A goal's .pr as it was read: its stat, when there was one.</summary>
    internal sealed record Stamp(global::app.type.item.path.@this.StatInfo? Stat);

    /// <summary>Whether <paramref name="pr"/> is as it was when this goal was read from it.</summary>
    internal async Task<bool> IsFresh(global::app.type.item.path.@this pr, actor.context.@this context)
    {
        if (Read is not { } read) return true;
        if (read.Stat is not { } then) return false;
        var now = await pr.Stat(context);
        return now.Success && await now.Value() is { } stat && stat.Same(then);
    }

    /// <summary>A goal starts through the goal type's events, then its own.</summary>
    protected internal override global::app.type.item.@this? Level(int depth, actor.context.@this context) => depth switch
    {
        0 => context.App.goal.type,
        1 => this,
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
    /// Visits every action in every step (ignoring Steps' disabled-skip iterator).
    /// Moves the "foreach step, foreach action" skeleton onto Goal so handlers can
    /// query the shape of a built goal without reaching through its children.
    /// </summary>
    public void ForEachAction(System.Action<Step, global::app.goal.step.action.@this> visitor)
    {
        foreach (var step in Step.Items())
            foreach (var action in step.Code.Items())
                visitor(step, action);
    }

    /// <summary>
    /// Parses .goal file text into a list of Child.
    /// The first goal holds the Path; the rest are its sub-goals, in its file. First goal is Public, rest are Private.
    /// Inverse of ToText().
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
        var currentGoal = (@this?)null;
        // The current goal's step node — steps Add straight into it. currentStep accumulates
        // (continuation lines rebuild it) and is Added at each boundary (next step / blank / goal / EOF).
        global::app.goal.step.list.@this? stepNode = null;
        var currentStep = (Step?)null;
        var pendingComment = new StringBuilder();
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
                        if (pendingComment.Length > 0) pendingComment.Append('\n');
                        pendingComment.Append(closePart);
                    }
                    inBlockComment = false;
                }
                else
                {
                    if (pendingComment.Length > 0) pendingComment.Append('\n');
                    pendingComment.Append(raw.Trim());
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
                    if (pendingComment.Length > 0) pendingComment.Append('\n');
                    pendingComment.Append(afterOpen[..closeIdx].Trim());
                }
                else
                {
                    inBlockComment = true;
                    if (pendingComment.Length > 0) pendingComment.Append('\n');
                    pendingComment.Append(afterOpen.Trim());
                }
                continue;
            }

            // Blank line — comment boundary
            if (string.IsNullOrWhiteSpace(raw))
            {
                pendingComment.Clear();
                if (currentStep != null) { stepNode!.Add(currentStep); currentStep = null; }
                continue;
            }

            // Line comment — any line starting with /
            if (trimmed.StartsWith("/"))
            {
                var commentText = trimmed[1..].TrimStart();
                if (pendingComment.Length > 0) pendingComment.Append('\n');
                pendingComment.Append(commentText);
                continue;
            }

            // Step line
            if (trimmed.StartsWith("- ") || trimmed == "-")
            {
                if (currentGoal == null)
                {
                    currentGoal = new @this(context.App.type.list)
                    {
                        Name = "Start",
                        Path = path
                    };
                    goals.Add(currentGoal);
                    stepNode = currentGoal.Step;
                    stepIndex = 0;
                }

                // Finalize the previous step before starting this one (continuations are done).
                if (currentStep != null) stepNode!.Add(currentStep);

                var leadingSpaces = raw.Length - raw.TrimStart().Length;
                var indent = leadingSpaces / 4;
                // The step's text is canonical at birth: every line trimmed at both ends (its \r gone),
                // continuation lines joined by \n — nothing inside a line changes. Readers compare and
                // print it as is (goal.Merge's `==`, the prompts), never trimming it again.
                var stepText = trimmed.Length > 2 ? trimmed[2..].Trim() : "";
                var comment = pendingComment.Length > 0 ? pendingComment.ToString() : null;
                pendingComment.Clear();

                currentStep = new Step(context.App.type.list)
                {
                    Goal = currentGoal,   // birth fact — the parser holds the goal, so it hands it over
                    Index = stepIndex,
                    Text = stepText,
                    Line = new global::app.goal.step.line.@this { Number = lineNumber, Indent = indent },
                    Comment = comment
                };

                stepIndex++;
                continue;
            }

            // Continuation line
            if (currentStep != null && raw.Length > 0 && raw[0] == ' ')
            {
                currentStep = new Step(context.App.type.list)
                {
                    Goal = currentStep.Goal,
                    Index = currentStep.Index,
                    Text = currentStep.Text + "\n" + trimmed.TrimEnd(),
                    Line = currentStep.Line,
                    Comment = currentStep.Comment
                };                continue;
            }

            // Escape character — \ at start of line continues previous step text
            if (currentStep != null && trimmed.StartsWith("\\"))
            {
                var escapedText = trimmed[1..].TrimEnd(); // strip the leading backslash
                currentStep = new Step(context.App.type.list)
                {
                    Goal = currentStep.Goal,
                    Index = currentStep.Index,
                    Text = currentStep.Text + "\n" + escapedText,
                    Line = currentStep.Line,
                    Comment = currentStep.Comment
                };                continue;
            }

            // Goal header
            var goalName = trimmed;
            var goalComment = pendingComment.Length > 0 ? pendingComment.ToString() : null;
            pendingComment.Clear();

            // Finalize the previous goal's last step before starting the new goal.
            if (currentStep != null) { stepNode?.Add(currentStep); currentStep = null; }

            // what the goal is — setup, system, a test — it answers itself from where it lives; a sub-goal lives in
            // its parent's file, so only the file's first goal holds the path
            currentGoal = new @this(context.App.type.list)
            {
                Name = goalName,
                Comment = goalComment,
                Path = goals.Count == 0 ? path : null,
            };
            goals.Add(currentGoal);
            stepNode = currentGoal.Step;
            stepIndex = 0;
            currentStep = null;
        }

        // Finalize the last step at EOF.
        if (currentStep != null) stepNode?.Add(currentStep);

        // Sub-goals nest under the root (public) goal
        if (goals.Count > 1)
        {
            for (int i = 1; i < goals.Count; i++)
                goals[0].Child.Add(goals[i]);
        }

        return goals[0];
    }

}
