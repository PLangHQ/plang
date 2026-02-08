using System.Text.Json.Serialization;
using PLang.Attributes;
using PLang.Runtime2.Serialization;

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
public sealed partial class Goal
{
    [Store, LlmBuilder, Debug, Default]
    public string Name { get; init; } = "";

    [Store, LlmBuilder, Debug, Default]
    public string? Description { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public string? Comment { get; init; }

    [Store, Debug, Default]
    public Steps Steps { get; init; } = new();

    [Store, Debug, Default]
    public List<string> SubGoals { get; init; } = new();

    [Store, LlmBuilder, Debug, Default]
    public Visibility Visibility { get; init; } = Visibility.Private;

    [Store, Debug]
    public string? Path { get; set; }

    [Store, Debug]
    public string? PrPath { get; set; }

    [Store, Debug]
    public string? Hash { get; init; }

    [Store, Debug, Default]
    public bool IsSetup { get; init; }

    [Store, Debug, Default]
    public bool IsEvent { get; init; }

    [Store, LlmBuilder, Debug, Default]
    public Dictionary<string, string>? InputParameters { get; init; }

    [LlmIgnore]
    [JsonIgnore]
    public Goal? Parent { get; set; }

    [Store, Debug]
    public List<Info> Errors { get; init; } = new();

    [Store, Debug]
    public List<Info> Warnings { get; init; } = new();

    [JsonIgnore]
    public EntityEvents Events { get; } = new();

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

    public static Goal NotFound(string name) => new()
    {
        Name = name,
        Description = "Goal not found"
    };

    public override string ToString() => Name;
}
