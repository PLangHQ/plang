using app.Utils;

namespace app.type.path.file;

/// <summary>
/// Concrete <c>file://</c> Path. Holds today's filesystem implementation
/// (verb methods, ctor, normalization). Subclass of the abstract
/// <see cref="app.type.path.@this"/> base. Constructed exclusively through
/// the scheme registry (<c>App.Types.Scheme.From</c>) or directly when a
/// caller already knows it wants a file Path (test fixtures, internal cleanup).
/// </summary>
[PathScheme("file")]
public sealed partial class @this : global::app.type.path.@this
{
    /// <summary>
    /// Constructs a FilePath. The incoming <paramref name="absolutePath"/> is
    /// canonicalized via <see cref="Canonicalize"/> before being stored, so
    /// every code path that produces a FilePath (Resolve, derivation verbs,
    /// scheme registry, implicit <c>string→path</c>) gets the canonical
    /// invariant for free.
    /// </summary>
    public @this(string absolutePath, actor.context.@this? context = null)
        : base(Canonicalize(absolutePath), context)
    {
    }

    // Invariant: Absolute always names the same OS file as the same
    // string handed to System.IO would. The permission gate's prefix-match
    // on Absolute is only sound when this holds — `..` and `.` segments
    // must be resolved before the string is stored.
    private static string Canonicalize(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
        // Relative inputs anchor to CWD inside GetFullPath, which would
        // silently change their identity. They never reach IO without first
        // being routed through a producer that knows the intended anchor.
        if (!PathHelper.IsPathRooted(absolutePath)) return absolutePath;
        // The "//x" prefix is an OS-rooted out-of-root form preserved
        // verbatim for idempotency under repeat normalization. GetFullPath
        // would collapse "//tmp/x" to "/tmp/x" and break that.
        if (absolutePath.StartsWith("//")) return absolutePath;
        // GetFullPath throws on inputs that can't be a real OS path
        // (ArgumentException, PathTooLongException, NotSupportedException,
        // SecurityException). Let those escape — the invariant above can't
        // hold for a string that isn't a path, and a silent fallback would
        // store a value whose textual form lies about what it points to.
        return PathHelper.GetFullPath(absolutePath);
    }

    [Out, Store] public override string Scheme => "file";

    // --- Live filesystem state — file-scheme-only (relocated off the base) ---
    //
    // These do synchronous System.IO calls and are meaningless for non-FS
    // schemes; they live on FilePath so an HttpPath never inherits them.
    // The cross-scheme liveness query is the async `Stat()`.

    /// <summary>True when a file or directory exists at this path.</summary>
    [LlmBuilder] public bool Exists =>
        System.IO.File.Exists(Absolute) || System.IO.Directory.Exists(Absolute);

    /// <summary>Size in bytes of the file at this path; 0 when absent.</summary>
    [LlmBuilder] public long Size
    {
        get
        {
            var info = new System.IO.FileInfo(Absolute);
            return info.Exists ? info.Length : 0;
        }
    }

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
            if (runtimeDir != null)
            {
                resolved = PathHelper.Combine(runtimeDir.Absolute, rawPath);
            }
            else
            {
                var goalPath = goal?.Path;
                if (goalPath != null)
                {
                    var goalDir = goalPath.Parent;
                    if (goalDir != null)
                        resolved = PathHelper.Combine(goalDir.Absolute, rawPath);
                }
            }
        }

        var p = new @this(ValidatePath(resolved, context.App), context) { Raw = rawPath };
        return p;
    }
}
