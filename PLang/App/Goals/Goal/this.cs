using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using App.Attributes;
using App.Events;
using App.modules;
using App.Utils;
namespace App.Goals.Goal;

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
public sealed partial class @this : modules.IDataWrappable
{
    private modules.Events? _events;
    [JsonIgnore]
    public modules.Events Events
    {
        get => _events ??= new modules.Events(this);
        set => _events = value;
    }
    [Store, LlmBuilder, Debug, Default]
    public string Name { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public string? Description { get; set; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    private Steps.@this _steps = new();
    [Store, Debug, Default]
    public Steps.@this Steps
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
    public string? Path { get; set; }

    /// <summary>
    /// On-disk .pr path the goal was loaded from. Set by GoalCall.LoadFromFile.
    /// Used by GetRuntimeDirectory to derive the goal's actual directory in the
    /// current App's filesystem — distinct from Path (which is the build-time
    /// identity, parent-perspective for goals run inside a child App).
    /// </summary>
    [JsonIgnore, LlmIgnore]
    public string? LoadedFromPrPath { get; set; }

    /// <summary>
    /// Returns the absolute on-disk directory that contains this goal's source
    /// .goal file in the current App's filesystem — derived from LoadedFromPrPath
    /// (a `<dir>/.build/<name>.pr`-shaped path) so it remains correct in child
    /// Apps where Path was baked from a different root. Returns null when the
    /// goal wasn't loaded from a file (in-memory goals built by tests / fixtures).
    /// </summary>
    public string? GetRuntimeDirectory()
    {
        if (App == null || string.IsNullOrEmpty(LoadedFromPrPath)) return null;
        var fs = App.FileSystem;
        string prAbs;
        try { prAbs = fs.ValidatePath(LoadedFromPrPath); }
        catch { return null; }
        var prParent = fs.Path.GetDirectoryName(prAbs);
        if (prParent == null) return null;
        if (!string.Equals(fs.Path.GetFileName(prParent), ".build", StringComparison.OrdinalIgnoreCase)) return null;
        return fs.Path.GetDirectoryName(prParent);
    }

    [Store, Debug]
    public string? PrPath
    {
        get
        {
            if (string.IsNullOrEmpty(Path)) return null;
            var sepIndex = Path.LastIndexOfAny(new[] { '\\', '/' });
            var dir = sepIndex >= 0 ? Path[..(sepIndex + 1)] : "";
            var fileName = sepIndex >= 0 ? Path[(sepIndex + 1)..] : Path;
            var dotIndex = fileName.LastIndexOf('.');
            var baseName = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
            return (dir + ".build" + (sepIndex >= 0 ? Path[sepIndex].ToString() : "/") + baseName.ToLowerInvariant() + ".pr").AdjustPathToOs();
        }
        init { } // PrPath is derived from Path; init-only so callers get compile errors instead of silent no-op
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
    public string FolderPath
    {
        get
        {
            if (string.IsNullOrEmpty(Path))
                return "/";

            var normalized = Path.Replace('\\', '/');
            var lastSep = normalized.LastIndexOf('/');
            if (lastSep <= 0)
                return "/";

            return normalized[..lastSep] + "/";
        }
    }

    [Store, LlmBuilder, Debug, Default]
    public Dictionary<string, string>? InputParameters { get; init; }

    [LlmIgnore]
    [JsonIgnore]
    public @this? Parent { get; set; }

    [LlmIgnore]
    [JsonIgnore]
    public App.@this? App { get; set; }

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
    public async Task<Data.@this> RunAsync(Actor.Context.@this context)
    {
        var previousGoal = context.Goal;
        context.Goal = this;

        if (context.CancellationToken.IsCancellationRequested)
            return Data.@this.FromError(new Errors.Error("Operation was cancelled", "Cancelled", 499));

        var lifecycle = context.LifecycleFor(this);

        // BeforeGoal events
        var beforeResult = await lifecycle.Before.Run(context, EventType.BeforeGoal);
        if (!beforeResult.Success) { context.Goal = previousGoal; return beforeResult; }
        if (beforeResult.Handled) { context.Goal = previousGoal; return beforeResult; }

        try
        {
            var result = await Steps.RunAsync(context);

            // Handle return depth
            if (result.Returned)
            {
                result.ReturnDepth--;
                if (result.ReturnDepth <= 0)
                    result.Returned = false;
            }

            // AfterGoal events
            var afterResult = await lifecycle.After.Run(context, EventType.AfterGoal);
            if (!afterResult.Success) return afterResult;

            return result;
        }
        finally
        {
            context.Goal = previousGoal;
        }
    }

    /// <summary>
    /// OBP: Goal is responsible for its own Data representation.
    /// Returns a cached per-execution Data&lt;Goal&gt; wrapper from the context.
    /// </summary>
    public Data.@this AsData(Actor.Context.@this context)
    {
        return context.GetOrCreate(this, () =>
        {
            var data = new Data.@this<@this>("", this);
            data.Context = context;
            return data;
        });
    }

    /// <summary>
    /// Groups modifier actions onto their preceding executable action for this goal's
    /// steps and every sub-goal recursively. Called before .pr serialization so saved
    /// files have modifiers correctly nested. Without recursion sub-goal steps keep
    /// modifiers flat and fail at runtime (flat modifiers' no-op Run wipes %__data__%).
    /// </summary>
    public void GroupModifiersRecursive(App.Modules.@this modules)
    {
        Steps.GroupAllModifiers(modules);
        foreach (var subGoal in Goals)
            subGoal.GroupModifiersRecursive(modules);
    }

    /// <summary>
    /// Runs this goal: lifecycle events → Steps.RunAsync → return handling.
    /// Context travels as parameter — goals may be cached/shared.
    /// </summary>
    public async Task<Data.@this> RunAsync(Actor.Context.@this context)
    {
        var previousGoal = context.Goal;
        context.Goal = this;

        if (context.CancellationToken.IsCancellationRequested)
            return Data.@this.FromError(new Errors.Error("Operation was cancelled", "Cancelled", 499));

        var lifecycle = context.LifecycleFor(this);

        // BeforeGoal events
        var beforeResult = await lifecycle.Before.Run(context, EventType.BeforeGoal);
        if (!beforeResult.Success) { context.Goal = previousGoal; return beforeResult; }
        if (beforeResult.Handled) { context.Goal = previousGoal; return beforeResult; }

        try
        {
            var result = await Steps.RunAsync(context);

            // Handle return depth
            if (result.Returned)
            {
                result.ReturnDepth--;
                if (result.ReturnDepth <= 0)
                    result.Returned = false;
            }

            // AfterGoal events
            var afterResult = await lifecycle.After.Run(context, EventType.AfterGoal);
            if (!afterResult.Success) return afterResult;

            return result;
        }
        finally
        {
            context.Goal = previousGoal;
        }
    }

    /// <summary>
    /// OBP: Goal is responsible for its own Data representation.
    /// Returns a cached per-execution Data&lt;Goal&gt; wrapper from the context.
    /// </summary>
    public Data.@this AsData(Actor.Context.@this context)
    {
        return context.GetOrCreate(this, () =>
        {
            var data = new Data.@this<@this>("", this);
            data.Context = context;
            return data;
        });
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
    public void ForEachAction(System.Action<Steps.Step.@this, Steps.Step.Actions.Action.@this> visitor)
    {
        foreach (var step in _steps.Value)
            foreach (var action in step.Actions)
                visitor(step, action);
    }

    /// <summary>
    /// Parses .goal file text into a list of Goals.
    /// All goals share the same Path. First goal is Public, rest are Private.
    /// Inverse of ToText().
    /// </summary>
    public static @this? Parse(string text, string path)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Replace("\t", "    ");

        var lines = text.Split('\n');
        var goals = new List<@this>();
        var currentGoal = (@this?)null;
        var currentStep = (Steps.Step.@this?)null;
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

                currentStep = new Steps.Step.@this
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
                currentStep = new Steps.Step.@this
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
                currentStep = new Steps.Step.@this
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

            var normalizedPath = path?.Replace('\\', '/').TrimStart('/') ?? "";
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
