using app.Utils;

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
    /// <summary>
    /// Constructs a FilePath. The incoming <paramref name="absolutePath"/> is
    /// canonicalized with <see cref="PathHelper.GetFullPath(string)"/> so
    /// <c>..</c>/<c>.</c> segments are resolved before being stored — this is
    /// the security F1 fix: <c>IsInRoot</c>'s textual prefix-match was
    /// bypassable with an un-canonicalized path. Canonicalizing here means
    /// every code path (Resolve, the derivation verbs, the scheme registry,
    /// the implicit <c>string→path</c> operator) inherits the fix for free.
    /// Pure string operation — no filesystem access.
    /// </summary>
    public @this(string absolutePath, actor.context.@this? context = null, object? content = null, string? source = null)
        : base(Canonicalize(absolutePath), context, content, source)
    {
    }

    private static string Canonicalize(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
        // Only canonicalize rooted inputs. Relative strings reach the ctor
        // only via the implicit string→path operator (test fixtures, in-memory
        // goals built from literals) — anchoring those to CWD would change
        // their identity unrelated to the F1 fix. The F1 attack requires a
        // rooted input (file.Resolve's Path.Combine of rooted runtimeDir +
        // relative ".." produces a rooted string with .. surviving).
        if (!PathHelper.IsPathRooted(absolutePath)) return absolutePath;
        // Preserve the OS-rooted "//x" prefix that ValidatePath keeps intact
        // for idempotency — GetFullPath would collapse "//tmp/x" to "/tmp/x".
        // Those paths are out-of-root and gated by Authorize regardless, so
        // the F1 attack doesn't apply.
        if (absolutePath.StartsWith("//")) return absolutePath;
        // Filter the catch to GetFullPath's actual failure modes — bare catch
        // would swallow OOM / StackOverflow / unexpected runtime issues and
        // hand AuthGate the un-canonical string silently. Anything outside
        // this list should fail loud. (codeanalyzer v2 N2)
        try { return PathHelper.GetFullPath(absolutePath); }
        catch (System.Exception ex) when (
            ex is System.ArgumentException
               or System.IO.PathTooLongException
               or System.NotSupportedException
               or System.Security.SecurityException)
        { return absolutePath; }
    }

    public override string Scheme => "file";

    // --- Live filesystem state — file-scheme-only (relocated off the base) ---
    //
    // These do synchronous System.IO calls and are meaningless for non-FS
    // schemes; they live on FilePath so an HttpPath never inherits them.
    // The cross-scheme liveness query is the async `Stat()`. (codeanalyzer v1 F2)

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
