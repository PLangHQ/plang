using App.Attributes;
using Goal = App.Goals.Goal.@this;

namespace App.Test;

/// <summary>
/// Metadata for a discovered *.test.goal file. Populated by test.discover.
/// Consumed by test.run, which turns each Ready TestFile into a TestRun.
/// </summary>
[PlangType("testfile")]
public sealed class TestFile
{
    /// <summary>Relative path of the .test.goal file from the app root.</summary>
    [LlmBuilder] public string Path { get; init; } = "";

    /// <summary>Relative path of the matching .pr file.</summary>
    [LlmBuilder] public string PrPath { get; init; } = "";

    /// <summary>Name of the entry goal to run (typically the first goal in the .pr).</summary>
    [LlmBuilder] public string EntryGoalName { get; init; } = "";

    /// <summary>Lifecycle status at the end of discovery: Ready, Stale, or Skipped.</summary>
    [LlmBuilder] public TestStatus Status { get; set; } = TestStatus.Ready;

    /// <summary>Absolute path of the directory containing the .test.goal file. test.run uses this as the per-test App's working directory.</summary>
    public string Directory { get; init; } = "";

    /// <summary>The goal loaded from the .pr (name, steps, actions, hash, builder version). Null if .pr missing or corrupt (Status == Stale).</summary>
    public Goal? Goal { get; init; }

    /// <summary>SHA-256 hash of (Name + concat(Step.Text)) as stored in the .pr at build time.</summary>
    public string? GoalHash { get; init; }

    /// <summary>Version of the builder that produced the .pr. Used for drift reporting.</summary>
    public string? BuilderVersion { get; init; }

    /// <summary>Union of user-declared tags (from test.tag at build-time) and auto-tags (from [RequiresCapability] on referenced handlers).</summary>
    public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Human-readable reason for non-Ready status (e.g., "no .pr", "rebuild needed").</summary>
    public string? StatusReason { get; set; }
}
