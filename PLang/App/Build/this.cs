using Force.DeepCloner;

namespace App.Build;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed partial class @this
{
    private readonly App.@this _app;

    /// <summary>
    /// Whether build mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional file filter. When set, only these files are built — IN ORDER.
    /// Set via --build={"files":"test.goal"} or --build={"files":["test.goal","run.goal"]}
    /// </summary>
    public List<FileSystem.Path> Files { get; set; } = new();

    /// <summary>
    /// Whether to use LLM cache. Default true. Set via --build={"cache":false}
    /// </summary>
    public bool Cache { get; set; } = true;

    /// <summary>
    /// Snapshot of .pr file content (raw JSON) loaded at first access during build.
    /// Keyed by absolute file path. When a .pr file is overwritten during build,
    /// the snapshot provides the original content for re-deserialization.
    /// </summary>
    private readonly Dictionary<string, string> _prSnapshot = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Snapshots .pr file content if not already captured.
    /// Called from file.Read paths during building.
    /// </summary>
    public void SnapshotPrFile(string absolutePath, string content)
    {
        _prSnapshot.TryAdd(absolutePath, content);
    }

    /// <summary>
    /// Gets snapshotted .pr file content. Returns null if not snapshotted.
    /// </summary>
    public string? GetPrSnapshot(string absolutePath)
    {
        return _prSnapshot.TryGetValue(absolutePath, out var content) ? content : null;
    }

    public @this(App.@this app)
    {
        _app = app;
    }
}
