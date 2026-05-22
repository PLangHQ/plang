namespace app.types.path.file;

/// <summary>
/// Concrete <c>file://</c> Path. Holds today's filesystem implementation
/// (verb methods, ctor, normalization). Subclass of the abstract
/// <see cref="app.types.path.@this"/> base. Constructed exclusively through
/// the scheme registry (<c>App.Types.Scheme.From</c>) or directly when a
/// caller already knows it wants a file Path (test fixtures, internal cleanup).
/// </summary>
[PathScheme("file")]
public sealed partial class @this : global::app.types.path.@this
{
    public @this(string absolutePath, actor.context.@this? context = null, object? content = null, string? source = null)
        : base(absolutePath, context, content, source)
    {
    }

    public override string Scheme => "file";

    /// <summary>
    /// FilePath-specific resolve: applies relative-path-to-goal-folder
    /// resolution and ValidatePath normalization. Called by the scheme
    /// registry's "file" factory. Bare paths (no scheme) also land here.
    /// </summary>
    public static new @this Resolve(string rawPath, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);

        var resolved = rawPath;

        // Relative paths resolve against the goal's folder. Prefer the runtime
        // directory derived from the .pr's on-disk location — Goal.Path is the
        // build-time identity (parent-perspective in child Apps) and would
        // mis-resolve. Fall back to Goal.Path's directory for in-memory goals
        // that have no LoadedFromPrPath.
        if (!rawPath.StartsWith('/') && !rawPath.StartsWith('\\') && !rawPath.Contains("://"))
        {
            var goal = context.Goal;
            var runtimeDir = goal?.GetRuntimeDirectory();
            if (!string.IsNullOrEmpty(runtimeDir))
            {
                resolved = System.IO.Path.Combine(runtimeDir, rawPath);
            }
            else
            {
                var goalPath = goal?.Path;
                if (!string.IsNullOrEmpty(goalPath))
                {
                    var goalDir = System.IO.Path.GetDirectoryName(goalPath);
                    if (!string.IsNullOrEmpty(goalDir))
                        resolved = System.IO.Path.Combine(goalDir, rawPath);
                }
            }
        }

        var p = new @this(ValidatePath(resolved, context.App), context) { Raw = rawPath };
        return p;
    }
}
