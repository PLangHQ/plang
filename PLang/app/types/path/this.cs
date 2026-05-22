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

            var rootAbsolutePath = RootAbsolutePath;
            var rootWithSeparator = rootAbsolutePath;
            if (!rootWithSeparator.EndsWith(System.IO.Path.DirectorySeparatorChar) && !rootWithSeparator.EndsWith(System.IO.Path.AltDirectorySeparatorChar))
                rootWithSeparator += System.IO.Path.DirectorySeparatorChar;

            if (_absolutePath.StartsWith(rootWithSeparator, RootComparison))
                _relative = _absolutePath[rootWithSeparator.Length..];
            else if (string.Equals(_absolutePath, rootAbsolutePath, RootComparison))
                _relative = ".";
            else
                _relative = _absolutePath;

            return _relative;
        }
    }

    [LlmBuilder] public string Extension => _extension ??= System.IO.Path.GetExtension(_absolutePath);
    [LlmBuilder] public string FileName => _fileName ??= System.IO.Path.GetFileName(_absolutePath);
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= System.IO.Path.GetFileNameWithoutExtension(_absolutePath);
    [LlmBuilder] public string Directory => _directory ??= System.IO.Path.GetDirectoryName(_absolutePath) ?? _absolutePath;
    [LlmBuilder] public string MimeType => Context?.App?.Formats?.Mime(Extension) ?? "application/octet-stream";

    [LlmBuilder] public bool IsFile => !string.IsNullOrEmpty(Extension);
    [LlmBuilder] public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// Converts this path to a GoalCall. Derives PrPath from the .goal file path.
    /// </summary>
    public GoalCall GoalCall
    {
        get
        {
            var rel = Relative.Replace('\\', '/');
            var dir = System.IO.Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var baseName = System.IO.Path.GetFileNameWithoutExtension(rel);
            var prDir = string.IsNullOrEmpty(dir) ? ".build" : $"{dir}/.build";
            var prPath = $"/{prDir}/{baseName.ToLowerInvariant()}.pr";
            return new GoalCall { Name = "", PrPath = prPath };
        }
    }

    // --- Live filesystem state ---
    //
    // The base deliberately exposes NO sync live-state property. `Exists` and
    // `Size` used to live here as `System.IO.File.Exists` / `FileInfo` calls —
    // wrong for an HttpPath (always-false / throws on Windows). They moved to
    // FilePath. The cross-scheme liveness query is the async `path.Stat()`;
    // truthiness ("does it exist") is `AsBooleanAsync()`. (codeanalyzer v1 F2)

    // --- Content (file content when set by provider, e.g., after file.read) ---

    public object? Content { get; set; }

    // --- Copy/move source tracking ---

    public string? Source { get; }

    // --- Display ---

    public override string ToString() => Content?.ToString() ?? Relative;

    public override bool Equals(object? obj) => obj switch
    {
        @this other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
        string str => string.Equals(_absolutePath, str, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);
}
