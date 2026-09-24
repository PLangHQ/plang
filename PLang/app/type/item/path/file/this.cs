using app.Utils;

namespace app.type.item.path.file;

/// <summary>
/// Concrete <c>file://</c> Path. Holds today's filesystem implementation
/// (verb methods, ctor, normalization). Subclass of the abstract
/// <see cref="app.type.item.path.@this"/> base. Constructed exclusively through
/// the scheme registry (<c>App.Types.Scheme.From</c>) or directly when a
/// caller already knows it wants a file Path (test fixtures, internal cleanup).
/// </summary>
[PathScheme("file")]
public sealed partial class @this : global::app.type.item.path.@this
{
    /// <summary>
    /// Constructs a FilePath. The incoming <paramref name="absolutePath"/> is
    /// canonicalized via <see cref="Canonicalize"/> before being stored, so
    /// every code path that produces a FilePath (Resolve, derivation verbs,
    /// scheme registry, implicit <c>string→path</c>) gets the canonical
    /// invariant for free.
    /// </summary>
    public @this(string absolutePath)
        : base(Canonicalize(absolutePath))
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
    [LlmBuilder] public global::app.type.item.number.@this Size
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

        // A file URL names an OS location — its local path, shown in plang form.
        if (global::app.type.item.path.scheme.@this.ParseScheme(rawPath).Equals("file", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(rawPath, UriKind.Absolute, out var url) && url.IsFile)
            return new @this(Canonicalize(url.LocalPath), context);

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

        var absolute = ValidatePath(resolved, context.App);
        // An OS location handed in (C# infra, a listing root) is shown in its plang form;
        // a location the developer typed is shown as typed.
        var isOsLocation = PathHelper.IsPathRooted(rawPath)
            && string.Equals(Canonicalize(rawPath), absolute, RootComparison);
        return isOsLocation ? new @this(absolute, context) : new @this(absolute) { Raw = rawPath };
    }

    /// <summary>An OS location, shown as a plang form under <paramref name="context"/>'s root —
    /// never the install root: "/x" under the app root, "/system/x" in the runtime's system
    /// folder, "//x" (or "c:/x") anywhere else. The context is not kept.</summary>
    private @this(string absolute, actor.context.@this context) : this(absolute)
    {
        var relative = Relative(context);
        var system = PathHelper.Combine(context.App.OsAbsolutePath, "system") + PathHelper.DirectorySeparatorChar;
        if (!string.Equals(relative, Absolute, StringComparison.Ordinal)) Raw = relative;
        else if (Absolute.StartsWith(system, RootComparison)) Raw = "/system/" + Absolute[system.Length..].Replace('\\', '/');
        else if (Absolute.StartsWith("//") || OperatingSystem.IsWindows()) Raw = Absolute.Replace('\\', '/');
        else Raw = "/" + Absolute;
    }
}
