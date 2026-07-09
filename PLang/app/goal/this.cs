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
    // A goal is a plain C# host — carried by plang as clr<goal>, navigated/written/read by
    // reflection off its [Store]/[Out] props (the * kind's Output/Read). No item.@this base.

    private module.Events? _events;
    [JsonIgnore]
    public module.Events Events
    {
        get => _events ??= new module.Events(this);
        set => _events = value;
    }
    [Store, LlmBuilder, Debug, Default]
    public string Name { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public string? Description { get; set; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    private steps.@this _steps = new();
    [Store, Debug, Default]
    public steps.@this Steps
    {
        get { _steps.Goal = this; return _steps; }
        init => _steps = value;
    }

    private List<@this> _goals = new();
    [Store, Debug, Default]
    public List<@this> Goals
    {
        get { foreach (var g in _goals) g.Parent ??= this; return _goals; }
        set => _goals = value;
    }

    [Store, LlmBuilder, Debug, Default]
    public Visibility Visibility { get; init; } = Visibility.Private;

    public override string ToString()
    {
        var sb = new StringBuilder(Name);
        foreach (var step in Steps)
        {
            sb.AppendLine();
            sb.Append(new string(' ', step.Indent * 4));
            sb.Append("- ");
            sb.Append(step.Text);
        }
        return sb.ToString();
    }

    [Store, Debug]
    public global::app.type.path.@this? Path { get; set; }

    /// <summary>
    /// On-disk .pr path the goal was loaded from. Set by GoalCall.LoadFromFile.
    /// Used by GetRuntimeDirectory to derive the goal's actual directory in the
    /// current App's filesystem — distinct from Path (which is the build-time
    /// identity, parent-perspective for goals run inside a child App).
    /// </summary>
    [JsonIgnore, LlmIgnore]
    public global::app.type.path.@this? LoadedFromPrPath { get; set; }

    /// <summary>
    /// Returns the on-disk directory that contains this goal's source
    /// .goal file in the current App's filesystem — derived from LoadedFromPrPath
    /// (a `<dir>/.build/<name>.pr`-shaped path) so it remains correct in child
    /// Apps where Path was baked from a different root. Returns null when the
    /// goal wasn't loaded from a file (in-memory goals built by tests / fixtures).
    /// </summary>
    public global::app.type.path.@this? GetRuntimeDirectory()
    {
        var pr = LoadedFromPrPath;
        if (pr == null) return null;
        var prParent = pr.Parent;
        if (prParent == null) return null;
        // The .build parent's own parent is the goal folder. Validate the .build
        // segment so naive in-memory paths don't quietly return the wrong dir.
        if (!string.Equals(prParent.FileName, ".build", StringComparison.OrdinalIgnoreCase)) return null;
        return prParent.Parent;
    }

    [Store, Debug]
    public global::app.type.path.@this? PrPath
    {
        get
        {
            // Empty or null Path → no PrPath. Treat "" the same as null so the
            // old IsNullOrEmpty(Path) → null shape (pre-Stage-3) still holds.
            if (Path == null || string.IsNullOrEmpty(Path.Absolute)) return null;
            // Derive via the generic verbs: parent dir → .build folder → lowercase stem + .pr
            var stem = Path.FileNameWithoutExtension.ToLowerInvariant();
            var parent = Path.Parent;
            if (parent == null) return null;
            return parent.Combine(".build").Combine(stem + ".pr");
        }
        init { } // PrPath is derived from Path; init no-op so JSON round-trip's serialized prPath is swallowed
    }

    [Store, Debug]
    public string? Hash
    {
        get
        {
            if (_hash != null) return _hash;
            if (Steps.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append(Name);
            foreach (var step in Steps)
                sb.Append(step.Text);

            _hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
            return _hash;
        }
        init => _hash = value;
    }
    private string? _hash;

    [Store, Debug]
    public string? BuilderVersion { get; set; }

    [Store, Debug, Default]
    public bool IsSetup { get; init; }

    [Store, Debug, Default]
    public bool IsEvent { get; init; }

    [Store, Debug, Default]
    public bool IsSystem { get; init; }

    [Store, Debug, Default]
    public bool IsTest { get; set; }


    /// <summary>
    /// Folder path of this goal, derived from Path.
    /// Starts with / (relative to app root, not OS root).
    /// E.g., \Cache\Start.goal → /Cache/, \Start.goal → /
    /// </summary>
    [JsonIgnore]
    [LlmIgnore]
    public global::app.type.path.@this? FolderPath
    {
        get => Path?.Parent;
    }

    [Store, LlmBuilder, Debug, Default]
    public Dictionary<string, string>? InputParameters { get; init; }

    [LlmIgnore]
    [JsonIgnore]
    public @this? Parent { get; set; }

    [LlmIgnore]
    [JsonIgnore]
    public app.@this App { get; set; } = null!;

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

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

    public string ToText()
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(Comment))
            lines.Add($"/ {Comment}");

        lines.Add(Name);

        foreach (var step in Steps)
        {
            if (!string.IsNullOrEmpty(step.Comment))
                lines.Add($"/ {step.Comment}");

            var prefix = new string(' ', step.Indent) + "- ";
            lines.Add(prefix + step.Text);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Merges LLM-derived fields from an existing built goal onto this freshly-parsed goal.
    /// Delegates to Steps.MergeFrom for this goal's steps, then recurses into sub-goals
    /// matched by Name so every sub-goal's steps also get their prior Actions + PriorText
    /// back. Without the recursion, @known / keep:true only fire for the top-level goal
    /// of a multi-goal .goal file.
    /// </summary>
    public void MergeFrom(@this? existing)
    {
        if (existing == null) return;
        Steps.MergeFrom(existing.Steps);

        if (existing.Goals.Count == 0) return;
        foreach (var subGoal in Goals)
        {
            var priorSub = existing.Goals.FirstOrDefault(g =>
                string.Equals(g.Name, subGoal.Name, StringComparison.OrdinalIgnoreCase));
            if (priorSub != null) subGoal.MergeFrom(priorSub);
        }

    }

    /// <summary>
    /// Runs this goal: lifecycle events → Steps.RunAsync → return handling.
    /// Context travels as parameter — goals may be cached/shared.
    /// </summary>
    public async Task<data.@this> RunAsync(actor.context.@this context)
    {
        var previousGoal = context.Goal;
        context.Goal = this;

        if (context.CancellationToken.IsCancellationRequested)
            return context.Error(new global::app.error.Error("Operation was cancelled", "Cancelled", 499));

        var lifecycle = context.LifecycleFor(this);

        // BeforeGoal events
        var beforeResult = await lifecycle.Before.Run(context, Trigger.BeforeGoal);
        if (!beforeResult.Success) { context.Goal = previousGoal; return beforeResult; }
        if (beforeResult.Handled) { context.Goal = previousGoal; return beforeResult; }

        // Goal-level Call frame. Step actions push under this; the goal frame outlives
        // any single action's pop, so things like `debug.tag` can attach metadata to a
        // scope that subsequent steps can still read (they navigate up via Current.Caller).
        // Cycle detection (ContainsGoal by PrPath) lives here too — entering a goal
        // already on the chain trips the overflow guard at this Push, before any step
        // action runs. Push lives INSIDE the try so a CallStackOverflowException becomes
        // Data.FromError instead of a raw CLR exception escaping RunAsync.
        //
        // Action.Step is pinned to Steps[0] solely to give ContainsGoal a Step→Goal anchor
        // for the cycle check (it reads action.Step?.Goal?.PrPath). This is the goal-entry
        // frame, not "step 0 running" — observers reading goalCall.Action.Step should treat
        // it as the goal anchor, not the currently-executing step (which is whatever the
        // child stepCall.Action.Step points at).
        var goalEntryAction = new global::app.goal.steps.step.actions.action.@this { Module = "goal", ActionName = "enter" };
        if (Steps.Count > 0) goalEntryAction.Step = Steps[0];

        try
        {
            await using var goalCall = context.CallStack.Push(goalEntryAction);

            var result = await Steps.RunAsync(context);

            // Handle return depth
            if (result.Returned)
            {
                result.ReturnDepth--;
                if (result.ReturnDepth <= 0)
                    result.Returned = false;
            }

            // AfterGoal events
            var afterResult = await lifecycle.After.Run(context, Trigger.AfterGoal);
            if (!afterResult.Success) return afterResult;

            return result;
        }
        catch (global::app.error.CallStackOverflowException ex)
        {
            // Cycle detection (depth limit or ContainsGoal) trips at Push, before the
            // goal frame is on the stack. Convert to ServiceError so Goal.RunAsync's
            // contract (returns Data, never throws) holds — outer Step.RunAsync's broad
            // catch would otherwise produce a ServiceError without goal/step context.
            var stack = context.CallStack;
            var caller = stack.Current;
            var chain = caller != null ? caller.SnapshotChain() : Array.Empty<global::app.callstack.call.@this>();
            var serviceErr = new global::app.error.ServiceError(
                ex.Message, goalEntryAction.Step!, chain, "CallStackOverflow", 500) { Exception = ex };
            stack.Audit.Add(serviceErr);
            return context.Error(serviceErr);
        }
        finally
        {
            context.Goal = previousGoal;
        }
    }

    /// <summary>
    /// Groups modifier actions onto their preceding executable action for this goal's
    /// steps and every sub-goal recursively. Called before .pr serialization so saved
    /// files have modifiers correctly nested. Without recursion sub-goal steps keep
    /// modifiers flat and fail at runtime (flat modifiers' no-op Run wipes %!data%).
    /// </summary>
    public void GroupModifiersRecursive(app.module.@this modules)
    {
        Steps.GroupAllModifiers(modules);
        foreach (var subGoal in Goals)
            subGoal.GroupModifiersRecursive(modules);
    }

    public static @this NotFound(string name) => new()
    {
        Name = name,
        Description = "Goal not found"
    };

    /// <summary>
    /// Visits every action in every step (ignoring Steps' disabled-skip iterator).
    /// Moves the "foreach step, foreach action" skeleton onto Goal so handlers can
    /// query the shape of a built goal without reaching through its children.
    /// </summary>
    public void ForEachAction(System.Action<Step, global::app.goal.steps.step.actions.action.@this> visitor)
    {
        foreach (var step in Steps)
            foreach (var action in step.Actions)
                visitor(step, action);
    }

    /// <summary>
    /// Parses .goal file text into a list of Goals.
    /// All goals share the same Path. First goal is Public, rest are Private.
    /// Inverse of ToText().
    /// </summary>
    public static @this? Parse(string text, global::app.type.path.@this? path)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Replace("\t", "    ");

        var lines = text.Split('\n');
        var goals = new List<@this>();
        var currentGoal = (@this?)null;
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
                currentStep = null;
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
                    currentGoal = new @this
                    {
                        Name = "Start",
                        Visibility = goals.Count == 0 ? Visibility.Public : Visibility.Private,
                        Path = path
                    };
                    goals.Add(currentGoal);
                    stepIndex = 0;
                }

                var leadingSpaces = raw.Length - raw.TrimStart().Length;
                var indent = leadingSpaces / 4;
                var stepText = trimmed.Length > 2 ? trimmed[2..] : "";
                var comment = pendingComment.Length > 0 ? pendingComment.ToString() : null;
                pendingComment.Clear();

                currentStep = new Step
                {
                    Index = stepIndex,
                    Text = stepText,
                    LineNumber = lineNumber,
                    Indent = indent,
                    Comment = comment
                };

                currentStep.Goal = currentGoal;
                currentGoal.Steps.Add(currentStep);
                stepIndex++;
                continue;
            }

            // Continuation line
            if (currentStep != null && raw.Length > 0 && raw[0] == ' ')
            {
                currentStep = new Step
                {
                    Index = currentStep.Index,
                    Text = currentStep.Text + "\n" + trimmed,
                    LineNumber = currentStep.LineNumber,
                    Indent = currentStep.Indent,
                    Comment = currentStep.Comment
                };
                currentGoal!.Steps[currentGoal.Steps.Count - 1] = currentStep;
                continue;
            }

            // Escape character — \ at start of line continues previous step text
            if (currentStep != null && trimmed.StartsWith("\\"))
            {
                var escapedText = trimmed[1..]; // strip the leading backslash
                currentStep = new Step
                {
                    Index = currentStep.Index,
                    Text = currentStep.Text + "\n" + escapedText,
                    LineNumber = currentStep.LineNumber,
                    Indent = currentStep.Indent,
                    Comment = currentStep.Comment
                };
                currentGoal!.Steps[currentGoal.Steps.Count - 1] = currentStep;
                continue;
            }

            // Goal header
            var goalName = trimmed;
            var goalComment = pendingComment.Length > 0 ? pendingComment.ToString() : null;
            pendingComment.Clear();

            var normalizedPath = path?.ToString().Replace('\\', '/').TrimStart('/') ?? "";
            var isSetup = goalName.Equals("Setup", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith("setup/", StringComparison.OrdinalIgnoreCase);
            var isSystem = normalizedPath.StartsWith("system/", StringComparison.OrdinalIgnoreCase);
            var isTest = normalizedPath.EndsWith(".test.goal", StringComparison.OrdinalIgnoreCase);

            currentGoal = new @this
            {
                Name = goalName,
                Comment = goalComment,
                Visibility = goals.Count == 0 ? Visibility.Public : Visibility.Private,
                Path = path,
                IsSetup = isSetup,
                IsSystem = isSystem,
                IsTest = isTest
            };
            goals.Add(currentGoal);
            stepIndex = 0;
            currentStep = null;
        }

        // Sub-goals nest under the root (public) goal
        if (goals.Count > 1)
        {
            for (int i = 1; i < goals.Count; i++)
                goals[0].Goals.Add(goals[i]);
        }

        return goals[0];
    }

}
