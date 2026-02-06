using PLang.Attributes;

namespace PLang.Runtime2.Core;

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
public sealed class Goal
{
    /// <summary>
    /// Name of the goal.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Description of what the goal does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Comment at the top of the goal file.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Steps in this goal.
    /// </summary>
    public List<Step> Steps { get; init; } = new();

    /// <summary>
    /// Sub-goals referenced by this goal.
    /// </summary>
    public List<string> SubGoals { get; init; } = new();

    /// <summary>
    /// Visibility of this goal.
    /// </summary>
    public Visibility Visibility { get; init; } = Visibility.Private;

    /// <summary>
    /// Relative path to the .goal file from app root.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Relative path to the compiled .pr file from app root.
    /// </summary>
    public string? PrPath { get; init; }

    /// <summary>
    /// Hash of the goal content (for caching/validation).
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// Whether this is a setup goal (runs once per session).
    /// </summary>
    public bool IsSetup { get; init; }

    /// <summary>
    /// Whether this is an event goal.
    /// </summary>
    public bool IsEvent { get; init; }

    /// <summary>
    /// Required input parameters.
    /// </summary>
    public Dictionary<string, string>? InputParameters { get; init; }

    /// <summary>
    /// Parent goal (if this is a sub-goal).
    /// </summary>
    [LlmIgnore]
    public Goal? Parent { get; set; }

    /// <summary>
    /// Errors encountered during building.
    /// </summary>
    public List<Info> Errors { get; init; } = new();

    /// <summary>
    /// Warnings from the build process.
    /// </summary>
    public List<Info> Warnings { get; init; } = new();

    /// <summary>
    /// Gets the full path including parent goals.
    /// </summary>
    public string FullPath
    {
        get
        {
            if (Parent == null)
                return Name;
            return $"{Parent.FullPath}/{Name}";
        }
    }

    /// <summary>
    /// Gets the goal as a formatted string.
    /// </summary>
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
    /// Creates a not-found placeholder goal.
    /// </summary>
    public static Goal NotFound(string name) => new()
    {
        Name = name,
        Description = "Goal not found"
    };

    public override string ToString() => Name;
}
