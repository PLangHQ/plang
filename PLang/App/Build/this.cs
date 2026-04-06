namespace App.Build;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed class @this
{
    private readonly App.@this _engine;

    /// <summary>
    /// Whether build mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Optional file filter. When set, only these files are built.
    /// Set via --build={"files":"test.goal"} or --build={"files":["test.goal","run.goal"]}
    /// </summary>
    public List<string> Files { get; set; } = new();

    public @this(App.@this engine)
    {
        _engine = engine;
    }
}
