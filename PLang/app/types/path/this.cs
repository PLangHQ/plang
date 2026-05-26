using app.Utils;
using app.Attributes;

namespace app.types.path;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Implements IContext for runtime graph access (FileSystem, etc.).
/// </summary>
[PlangType("path",
    Example = "/some/file.json")]
public abstract partial class @this : modules.IContext, global::app.data.IBooleanResolvable
{
    /// <summary>
    /// Scheme name for this path (e.g. "file", "http", "https"). Subclasses
    /// implement. Used by Permission canonical-form and diagnostic surfaces.
    /// </summary>
    public abstract string Scheme { get; }


    /// <summary>
    /// String comparison for "is this path under that root" checks. Linux
    /// filesystems are case-sensitive — comparing case-insensitively lets
    /// <c>/SRV/myapp</c> match <c>/srv/myapp</c> and slip past the gate.
    /// Windows is case-insensitive at the filesystem layer, so we honour
    /// that. Single home so <see cref="IsUnder"/> and
    /// <c>PLangFileSystem.ValidatePath</c> can't drift apart again.
    /// </summary>
    internal static StringComparison RootComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    protected readonly string _absolutePath;

    // Cached string-derived properties
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;
    private string? _relative;

    /// <summary>
    /// App root directory — resolved from Context. The string-derived path
    /// properties (Relative, Extension, …) need it; they throw if accessed
    /// before Context is wired (only CLI-parse-time / direct test construction
    /// skip it — by any real flow Context is always available).
    /// </summary>
    private string RootAbsolutePath => Context?.App?.AbsolutePath
        ?? throw new InvalidOperationException(
            "Path requires Context with App — wire it before accessing path-derived properties");

    /// <summary>
    /// Creates a Path. Context is optional at construction so utilities that
    /// build Paths before runtime is up (CLI parser, source generators) can
    /// construct them; Context arrives via the IContext setter when the Path
    /// is wrapped in Data&lt;Path&gt; or set on a runtime object.
    /// </summary>
    protected @this(string absolutePath, actor.context.@this? context = null, object? content = null, string? source = null)
    {
        _absolutePath = absolutePath;
        Context = context;
        Content = content;
        Source = source;
    }

    /// <summary>
    /// Context for runtime access. Settable through IContext (Data propagates
    /// it automatically when Path is inside Data&lt;Path&gt;).
    /// JsonIgnore — Context references the runtime graph (App, Culture, parents)
    /// which contains cycles; serializing it blows up trace files.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    /// <summary>
    /// Source generator convention — auto-wraps string parameters. Routes
    /// through the per-App scheme registry so the right subclass is built
    /// (file → FilePath, http → HttpPath, ...). Bare paths default to file.
    /// </summary>
    public static @this Resolve(string rawPath, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);
        return context.App.Types.Scheme.From(rawPath, context);
    }

    // --- Path properties ---

    public string Raw { get; init; } = "";
    public virtual string Absolute => _absolutePath;

    public string Relative
    {
        get
        {
            if (_relative != null) return _relative;

            // No Context (test fixtures, JSON deserialize without scope) — no root
            // anchor, so the portable form is just Raw or absolute as-is.
            if (Context?.App == null)
                return _relative = !string.IsNullOrEmpty(Raw) ? Raw : _absolutePath;

            var rootAbsolutePath = RootAbsolutePath;
            var rootWithSeparator = rootAbsolutePath;
            if (!rootWithSeparator.EndsWith(PathHelper.DirectorySeparatorChar) && !rootWithSeparator.EndsWith(PathHelper.AltDirectorySeparatorChar))
                rootWithSeparator += PathHelper.DirectorySeparatorChar;

            // Canonical PLang root-relative form: leading "/" anchors at the
            // app root, "/" as separator regardless of OS (matches Goal.Path
            // / GoalCall.PrPath stored in .pr files). Out-of-root paths
            // return their Absolute form unchanged — those aren't "relative
            // to root" in any meaningful sense.
            if (_absolutePath.StartsWith(rootWithSeparator, RootComparison))
                _relative = "/" + _absolutePath[rootWithSeparator.Length..].Replace('\\', '/');
            else if (string.Equals(_absolutePath, rootAbsolutePath, RootComparison))
                _relative = "/";
            else
                _relative = _absolutePath;

            return _relative;
        }
    }

    [LlmBuilder] public string Extension => _extension ??= PathHelper.GetExtension(_absolutePath);
    [LlmBuilder] public string FileName => _fileName ??= PathHelper.GetFileName(_absolutePath);
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= PathHelper.GetFileNameWithoutExtension(_absolutePath);
    [LlmBuilder] public string Directory => _directory ??= PathHelper.GetDirectoryName(_absolutePath) ?? _absolutePath;
    [LlmBuilder] public string MimeType => Context?.App?.Formats?.Mime(Extension) ?? "application/octet-stream";

    [LlmBuilder] public bool IsFile => !string.IsNullOrEmpty(Extension);
    [LlmBuilder] public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// Converts this path to a GoalCall. Derives PrPath from the .goal file path.
    /// Excluded from default JSON serialization — it builds a new GoalCall (which
    /// holds a Path which has a GoalCall ...) and would cycle infinitely when a
    /// caller serializes a Goal without the PathJsonConverter registered.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public GoalCall GoalCall
    {
        get
        {
            // Derive the .pr sibling path via the generic derivation verbs.
            // .goal-file → parent/.build/<lowercase-stem>.pr.
            var stem = FileNameWithoutExtension.ToLowerInvariant();
            var parent = Parent;
            var prPath = parent != null
                ? parent.Combine(".build").Combine(stem + ".pr")
                : this.Combine(".build").Combine(stem + ".pr");
            return new GoalCall { Name = "", PrPath = prPath };
        }
    }

    // --- Live filesystem state ---
    //
    // The base deliberately exposes NO sync live-state property. `Exists` and
    // `Size` used to live here as `System.IO.File.Exists` / `FileInfo` calls —
    // wrong for an HttpPath (always-false / throws on Windows). They moved to
    // FilePath. The cross-scheme liveness query is the async `path.Stat()`;
    // truthiness ("does it exist") is `AsBooleanAsync()`.

    // --- Content (file content when set by provider, e.g., after file.read) ---

    public object? Content { get; set; }

    // --- Copy/move source tracking ---

    public string? Source { get; }

    // --- Display ---

    public override string ToString()
    {
        if (Content?.ToString() is { } c) return c;
        // No Context means Relative would throw — fall back to the raw or
        // absolute form so dictionary-keying / interpolation of stub Paths
        // (test fixtures, JSON deserialize without scope) still works.
        if (Context == null) return !string.IsNullOrEmpty(Raw) ? Raw : _absolutePath;
        try { return Relative; } catch { return !string.IsNullOrEmpty(Raw) ? Raw : _absolutePath; }
    }

    // Path equality follows RootComparison — the same case-sensitivity rule
    // Relative/IsUnder/ValidatePath use, so they can't drift apart. Hard-coding
    // OrdinalIgnoreCase here would make /srv/x and /SRV/x — distinct files on
    // Linux — compare equal and hash-collide.
    public override bool Equals(object? obj) => obj switch
    {
        @this other => string.Equals(_absolutePath, other._absolutePath, RootComparison),
        string str => string.Equals(_absolutePath, str, RootComparison),
        _ => false
    };

    public override int GetHashCode() =>
        StringComparer.FromComparison(RootComparison).GetHashCode(_absolutePath);

    /// <summary>
    /// Convenience: <c>"some/file.goal"</c> automatically lifts to a file-scheme
    /// Path with Context=null. Use sites are test fixtures, in-memory Goals
    /// built from string literals, and JSON deserialize paths that don't have
    /// a Context available. Production code with a Context in scope should go
    /// through <see cref="Resolve(string, actor.context.@this)"/> instead so the
    /// scheme registry picks the right subclass and Context wires immediately.
    /// </summary>
    public static implicit operator @this(string raw)
        => new file.@this(raw) { Raw = raw };

    /// <summary>
    /// A Path implicitly stringifies to its <see cref="ToString"/> representation.
    /// Lets <c>Assert.That(path).IsEqualTo("/some/path")</c> compile as a
    /// string-vs-string check (with the right value surfaced in failure messages),
    /// and rescues string interpolation across third-party libs that don't call
    /// ToString themselves. Returns null for null Path so null-aware assertions
    /// (e.g. <c>IsNull()</c>) don't get fooled by the implicit conversion into
    /// reading an empty string as "found a value".
    /// </summary>
    public static implicit operator string?(@this? p) => p?.ToString();
}
