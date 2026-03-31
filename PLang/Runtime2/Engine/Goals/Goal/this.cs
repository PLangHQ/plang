using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using PLang.Attributes;
namespace PLang.Runtime2.Engine.Goals.Goal;

/// <summary>
/// Visibility of a goal.
/// </summary>
public enum Visibility
{
    Private = 0,
    Public = 1
}

/// <summary>
/// Represents a goal (a .goal file or sub-goal) for Runtime2.
/// </summary>
public sealed partial class @this
{
    [Store, LlmBuilder, Debug, Default]
    public string Name { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public string? Description { get; set; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    [Store, Debug, Default]
    public Steps.@this Steps { get; init; } = new();

    [Store, Debug, Default]
    public List<string> SubGoals { get; init; } = new();

    [Store, LlmBuilder, Debug, Default]
    public Visibility Visibility { get; init; } = Visibility.Private;

    [Store, Debug]
    public string? Path { get; set; }

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
            return dir + ".build" + (sepIndex >= 0 ? Path[sepIndex].ToString() : "\\") + baseName.ToLowerInvariant() + ".pr";
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
    /// Starts with / (relative to engine root, not OS root).
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
    public Engine.@this? Engine { get; set; }

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
    /// Matches steps by Text, delegates to Step.Merge for each match.
    /// </summary>
    public void MergeFrom(@this? existing)
    {
        if (existing == null || existing.Steps.Count == 0) return;

        var consumed = new HashSet<int>();
        foreach (var step in Steps)
        {
            for (int i = 0; i < existing.Steps.Count; i++)
            {
                if (consumed.Contains(i)) continue;
                if (existing.Steps[i].Text == step.Text)
                {
                    step.Merge(existing.Steps[i]);
                    consumed.Add(i);
                    break;
                }
            }
        }
    }

    public static @this NotFound(string name) => new()
    {
        Name = name,
        Description = "Goal not found"
    };

    /// <summary>
    /// Parses .goal file text into a list of Goals.
    /// All goals share the same Path. First goal is Public, rest are Private.
    /// Inverse of ToText().
    /// </summary>
    public static List<@this> Parse(string text, string path)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<@this>();

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

            currentGoal = new @this
            {
                Name = goalName,
                Comment = goalComment,
                Visibility = goals.Count == 0 ? Visibility.Public : Visibility.Private,
                Path = path,
                IsSetup = isSetup,
                IsSystem = isSystem
            };
            goals.Add(currentGoal);
            stepIndex = 0;
            currentStep = null;
        }

        // Populate SubGoals on the first (public) goal
        if (goals.Count > 1)
        {
            for (int i = 1; i < goals.Count; i++)
                goals[0].SubGoals.Add(goals[i].Name);
        }

        return goals;
    }

}
