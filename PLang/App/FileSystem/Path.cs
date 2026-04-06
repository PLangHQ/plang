using App.Variables;
using App.Utils;

namespace App.FileSystem;

/// <summary>
/// Path that IS a Data — flows through PLang as a variable.
/// FileSystem is resolved lazily from Context.App.FileSystem.
/// Exists and Size are live (lazy filesystem checks).
/// Value holds file content when set by the provider (e.g., Read).
/// </summary>
public class Path : Data.@this
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
        : base("", content, content != null ? Data.Type.FromMime(TypeMapping.GetMimeType(
            global::System.IO.Path.GetExtension(absolutePath))) : null)
    {
        _absolutePath = absolutePath;
        Source = source;
    }

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    public static Path Resolve(string rawPath, Context.@this context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);

        var fs = context.App.FileSystem;
        var resolved = rawPath;

        // Relative paths resolve against the goal's folder
        if (!rawPath.StartsWith('/') && !rawPath.StartsWith('\\') && !rawPath.Contains("://"))
        {
            var goalPath = context.Goal?.Path;
            if (!string.IsNullOrEmpty(goalPath))
            {
                var goalDir = fs.Path.GetDirectoryName(goalPath);
                if (!string.IsNullOrEmpty(goalDir))
                    resolved = fs.Path.Combine(goalDir, rawPath);
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

    // --- Copy/move source tracking ---

    public string? Source { get; }

    // --- ToBoolean ---

    public override bool ToBoolean() => Exists;

    // --- Display ---

    public override string ToString() => Value?.ToString() ?? Relative;

    public override bool Equals(object? obj) => obj switch
    {
        Path other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
        string str => string.Equals(_absolutePath, str, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);

    public override Data.@this Clone()
    {
        var clone = new Path(_absolutePath, Value, Source)
        {
            Name = Name,
            Properties = Properties.Clone(),
            Context = Context
        };
        clone.Error = Error;
        clone.Handled = Handled;
        clone.Warnings = Warnings != null ? new List<Info>(Warnings) : null;
        clone.Signature = Signature;
        return clone;
    }
}
