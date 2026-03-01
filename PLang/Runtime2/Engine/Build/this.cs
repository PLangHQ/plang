namespace PLang.Runtime2.Engine.Build;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed class @this
{
    private readonly Engine.@this _engine;

    /// <summary>
    /// Whether build mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    public @this(Engine.@this engine)
    {
        _engine = engine;
    }
}
