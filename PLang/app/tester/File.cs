using Goal = app.goals.goal.@this;

namespace app.tester;

/// <summary>
/// Discovery record for a <c>*.test.goal</c> file. Populated by test.discover.
/// Consumed by test.run, which turns each Ready File into a Run.
/// Identity (path, name, hash, builder version, folder) lives on
/// <see cref="Goal"/>; this record owns the discovery-only state.
/// </summary>
public sealed class File
{
    /// <summary>The discovered goal. Always populated — built from the .pr
    /// when available, otherwise parsed from the .goal source itself.</summary>
    public required Goal Goal { get; init; }

    /// <summary>Lifecycle status at the end of discovery.</summary>
    public Status Status { get; set; } = Status.Ready;

    /// <summary>Human-readable reason for non-Ready status (e.g., "no .pr", "rebuild needed").</summary>
    public string? StatusReason { get; set; }

    /// <summary>Union of user-declared tags (test.tag at build-time) and auto-tags (handler [RequiresCapability]).</summary>
    public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
}
