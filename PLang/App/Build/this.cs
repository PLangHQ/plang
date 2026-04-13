using Force.DeepCloner;

namespace App.Build;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed class @this
{
    private readonly App.@this _app;

    /// <summary>
    /// Whether build mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional file filter. When set, only these files are built.
    /// Set via --build={"files":"test.goal"} or --build={"files":["test.goal","run.goal"]}
    /// </summary>
    public List<FileSystem.Path> Files { get; set; } = new();

    /// <summary>
    /// Whether to use LLM cache. Default true. Set via --build={"cache":false}
    /// </summary>
    public bool Cache { get; set; } = true;

    /// <summary>
    /// Snapshot of system goals loaded at build start.
    /// During building, goal resolution uses these instead of reading from disk,
    /// so newly-saved .pr files don't affect the running build.
    /// </summary>
    private readonly Dictionary<string, Goals.Goal.@this> _goalSnapshot = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Snapshots a goal so it won't be affected by .pr overwrites during the build.
    /// Called when goals are first loaded at build start.
    /// </summary>
    public void SnapshotGoal(Goals.Goal.@this goal)
    {
        if (!string.IsNullOrEmpty(goal.PrPath))
            _goalSnapshot[goal.PrPath] = goal.DeepClone();
    }

    /// <summary>
    /// Gets a snapshotted goal by name or PrPath. Returns null if not snapshotted.
    /// </summary>
    public Goals.Goal.@this? GetSnapshot(string nameOrPath)
    {
        if (_goalSnapshot.TryGetValue(nameOrPath, out var goal)) return goal;
        // Try matching by goal name
        foreach (var g in _goalSnapshot.Values)
        {
            if (string.Equals(g.Name, nameOrPath, StringComparison.OrdinalIgnoreCase))
                return g;
        }
        return null;
    }

    /// <summary>
    /// Snapshots all currently loaded goals. Called when building starts,
    /// so goals loaded at startup are preserved during the build.
    /// </summary>
    public void SnapshotAll()
    {
        foreach (var goal in _app.Goals.AllIncludingSetup)
        {
            SnapshotGoal(goal);
        }
    }

    public @this(App.@this app)
    {
        _app = app;
    }
}
