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

    public @this(App.@this app)
    {
        _app = app;
    }
}
