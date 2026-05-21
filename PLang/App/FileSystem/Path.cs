using App.Utils;
using App.Attributes;

namespace App.FileSystem;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Implements IContext for runtime graph access (FileSystem, etc.).
/// </summary>
[PlangType("path",
    Example = "/some/file.json")]
public partial class Path : modules.IContext
{
    private readonly string _absolutePath;

    // Cached string-derived properties
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;
    private string? _relative;

    /// <summary>
    /// Lazy FileSystem access — resolved from Context.App.FileSystem.
    /// Context can be supplied via constructor, or set later through the IContext
    /// interface (Data&lt;Path&gt;.Context propagation). Properties that need the
    /// filesystem (Relative, Extension, FileName, etc.) throw if accessed before
    /// Context is wired — by that point in any real flow Context is always
    /// available; only CLI-parse-time and direct test construction skip it.
    /// </summary>
    private IPLangFileSystem Fs => Context?.App?.FileSystem
        ?? throw new InvalidOperationException(
            "Path requires Context with App.FileSystem — wire it before accessing filesystem-dependent properties");

    /// <summary>
    /// Creates a Path. Context is optional at construction so utilities that
    /// build Paths before runtime is up (CLI parser, source generators) can
    /// construct them; Context arrives via the IContext setter when the Path
    /// is wrapped in Data&lt;Path&gt; or set on a runtime object.
    /// </summary>
    public Path(string absolutePath, Actor.Context.@this? context = null, object? content = null, string? source = null)
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
    public Actor.Context.@this? Context { get; set; }

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    public static Path Resolve(string rawPath, Actor.Context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);

        var fs = context.App.FileSystem;
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
                resolved = fs.Path.Combine(runtimeDir, rawPath);
            }
            else
            {
                var goalPath = goal?.Path;
                if (!string.IsNullOrEmpty(goalPath))
                {
                    var goalDir = fs.Path.GetDirectoryName(goalPath);
                    if (!string.IsNullOrEmpty(goalDir))
                        resolved = fs.Path.Combine(goalDir, rawPath);
                }
            }
        }

        var path = new Path(fs.ValidatePath(resolved), context) { Raw = rawPath };
        return path;
    }

    // --- Path properties ---

    public string Raw { get; init; } = "";
    public string Absolute => _absolutePath;

    public string Relative
    {
        get
        {
            if (_relative != null) return _relative;

            var root = Fs.RootDirectory;
            if (!root.EndsWith(Fs.Path.DirectorySeparatorChar) && !root.EndsWith(Fs.Path.AltDirectorySeparatorChar))
                root += Fs.Path.DirectorySeparatorChar;

            if (_absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                _relative = _absolutePath[root.Length..];
            else if (string.Equals(_absolutePath, Fs.RootDirectory, StringComparison.OrdinalIgnoreCase))
                _relative = ".";
            else
                _relative = _absolutePath;

            return _relative;
        }
    }

    [LlmBuilder] public string Extension => _extension ??= Fs.Path.GetExtension(_absolutePath);
    [LlmBuilder] public string FileName => _fileName ??= Fs.Path.GetFileName(_absolutePath);
    [LlmBuilder] public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= Fs.Path.GetFileNameWithoutExtension(_absolutePath);
    [LlmBuilder] public string Directory => _directory ??= Fs.Path.GetDirectoryName(_absolutePath) ?? _absolutePath;
    [LlmBuilder] public string MimeType => Context?.App?.Formats?.Mime(Extension) ?? "application/octet-stream";

    [LlmBuilder] public bool IsFile => !string.IsNullOrEmpty(Extension);
    [LlmBuilder] public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// Converts this path to a GoalCall. Derives PrPath from the .goal file path.
    /// </summary>
    public Goals.Goal.GoalCall GoalCall
    {
        get
        {
            var rel = Relative.Replace('\\', '/');
            var dir = Fs.Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var baseName = Fs.Path.GetFileNameWithoutExtension(rel);
            var prDir = string.IsNullOrEmpty(dir) ? ".build" : $"{dir}/.build";
            var prPath = $"/{prDir}/{baseName.ToLowerInvariant()}.pr";
            return new Goals.Goal.GoalCall { Name = "", PrPath = prPath };
        }
    }

    // --- Live filesystem properties ---

    [LlmBuilder] public bool Exists => Fs.File.Exists(_absolutePath) || Fs.Directory.Exists(_absolutePath);

    [LlmBuilder] public long Size
    {
        get
        {
            var info = Fs.FileInfo.New(_absolutePath);
            return info.Exists ? info.Length : 0;
        }
    }

    // --- Content (file content when set by provider, e.g., after file.read) ---

    public object? Content { get; set; }

    // --- Copy/move source tracking ---

    public string? Source { get; }

    // --- Display ---

    public override string ToString() => Content?.ToString() ?? Relative;

    public override bool Equals(object? obj) => obj switch
    {
        Path other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
        string str => string.Equals(_absolutePath, str, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);
}
