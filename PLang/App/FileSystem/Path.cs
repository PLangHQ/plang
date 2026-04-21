using App.Utils;

namespace App.FileSystem;

/// <summary>
/// Plain domain class representing a filesystem path.
/// NOT a Data subclass — wrapped in Data&lt;Path&gt; by handlers.
/// Implements IContext for runtime graph access (FileSystem, etc.).
/// </summary>
public class Path : modules.IContext
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
    /// </summary>
    private IPLangFileSystem Fs => Context?.App?.FileSystem
        ?? throw new InvalidOperationException("Path requires Context with App.FileSystem");

    /// <summary>
    /// Creates a Path from an absolute path with optional content.
    /// </summary>
    public Path(string absolutePath, object? content = null, string? source = null)
    {
        _absolutePath = absolutePath;
        Content = content;
        Source = source;
    }

    /// <summary>
    /// Context for runtime access. Set via IContext interface.
    /// Data propagates this automatically when Path is inside Data&lt;Path&gt;.
    /// </summary>
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

        var path = new Path(fs.ValidatePath(resolved)) { Raw = rawPath, Context = context };
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

    public string Extension => _extension ??= Fs.Path.GetExtension(_absolutePath);
    public string FileName => _fileName ??= Fs.Path.GetFileName(_absolutePath);
    public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= Fs.Path.GetFileNameWithoutExtension(_absolutePath);
    public string Directory => _directory ??= Fs.Path.GetDirectoryName(_absolutePath) ?? _absolutePath;
    public string MimeType => TypeMapping.GetMimeType(Extension);

    public bool IsFile => !string.IsNullOrEmpty(Extension);
    public bool IsDirectory => string.IsNullOrEmpty(Extension);

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

    public bool Exists => Fs.File.Exists(_absolutePath) || Fs.Directory.Exists(_absolutePath);

    public long Size
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
