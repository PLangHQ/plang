using PLang.Interfaces;
using App.Engine.Variables;
using App.Engine.Utility;
using PLangContext = App.Engine.Context.PLangContext;

namespace App.Engine.FileSystem;

/// <summary>
/// Path that IS a Data — flows through PLang as a variable.
/// Resolution/metadata from the old Path class + Data inheritance.
/// Exists and Size are live (lazy filesystem checks).
/// Value holds file content when set by the provider (e.g., Read).
/// </summary>
public class Path : Data
{
    private readonly IPLangFileSystem _fs;
    private readonly string _absolutePath;

    // Cached string-derived properties
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;
    private string? _relative;

    /// <summary>
    /// Creates a Path from an absolute path. Used by providers to build results.
    /// </summary>
    public Path(string absolutePath, IPLangFileSystem fs, object? content = null, string? source = null)
        : base("", content, content != null ? Variables.Type.FromMime(TypeMapping.GetMimeType(
            fs.Path.GetExtension(absolutePath))) : null)
    {
        _fs = fs;
        _absolutePath = fs.ValidatePath(absolutePath);
        Source = source;
    }

    /// <summary>
    /// Creates a Path from a raw path string, resolving relative paths against the goal folder.
    /// Used by the source generator via Resolve().
    /// </summary>
    public Path(string rawPath, PLangContext context)
        : base("", null)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);

        Raw = rawPath;
        _fs = context.Engine.FileSystem;

        // Relative paths resolve against the goal's folder
        var resolved = rawPath;
        if (!rawPath.StartsWith('/') && !rawPath.StartsWith('\\') && !rawPath.Contains("://"))
        {
            var goalPath = context.Goal?.Path;
            if (!string.IsNullOrEmpty(goalPath))
            {
                var goalDir = _fs.Path.GetDirectoryName(goalPath);
                if (!string.IsNullOrEmpty(goalDir))
                    resolved = _fs.Path.Combine(goalDir, rawPath);
            }
        }

        _absolutePath = _fs.ValidatePath(resolved);
    }

    /// <summary>Source generator convention — auto-wraps string parameters.</summary>
    public static Path Resolve(string rawPath, PLangContext context)
        => new Path(rawPath, context);

    // --- Path properties ---

    public string Raw { get; } = "";
    public string Absolute => _absolutePath;

    public string Relative
    {
        get
        {
            if (_relative != null) return _relative;

            var root = _fs.RootDirectory;
            if (!root.EndsWith(_fs.Path.DirectorySeparatorChar) && !root.EndsWith(_fs.Path.AltDirectorySeparatorChar))
                root += _fs.Path.DirectorySeparatorChar;

            if (_absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                _relative = _absolutePath[root.Length..];
            else if (string.Equals(_absolutePath, _fs.RootDirectory, StringComparison.OrdinalIgnoreCase))
                _relative = ".";
            else
                _relative = _absolutePath;

            return _relative;
        }
    }

    public string Extension => _extension ??= _fs.Path.GetExtension(_absolutePath);
    public string FileName => _fileName ??= _fs.Path.GetFileName(_absolutePath);
    public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= _fs.Path.GetFileNameWithoutExtension(_absolutePath);
    public string Directory => _directory ??= _fs.Path.GetDirectoryName(_absolutePath) ?? _absolutePath;
    public string MimeType => TypeMapping.GetMimeType(Extension);

    public bool IsFile => !string.IsNullOrEmpty(Extension);
    public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>
    /// Converts this path to a GoalCall. Derives PrPath from the .goal file path.
    /// E.g., "SettingsCrud/Start.test.goal" → GoalCall { Name = "Start", PrPath = "/SettingsCrud/.build/start.test.pr" }
    /// </summary>
    public Goals.Goal.GoalCall GoalCall
    {
        get
        {
            var rel = Relative.Replace('\\', '/');
            var dir = _fs.Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var baseName = _fs.Path.GetFileNameWithoutExtension(rel);
            // Build PrPath: dir/.build/basename.pr
            var prDir = string.IsNullOrEmpty(dir) ? ".build" : $"{dir}/.build";
            var prPath = $"/{prDir}/{baseName.ToLowerInvariant()}.pr";
            return new Goals.Goal.GoalCall { Name = "", PrPath = prPath };
        }
    }

    // --- Live filesystem properties ---

    public bool Exists => _fs.File.Exists(_absolutePath) || _fs.Directory.Exists(_absolutePath);

    public long Size
    {
        get
        {
            var info = _fs.FileInfo.New(_absolutePath);
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

    public override Data Clone()
    {
        var clone = new Path(_absolutePath, _fs, Value, Source)
        {
            Name = Name,
            Properties = Properties.Clone()
        };
        clone.Error = Error;
        clone.Handled = Handled;
        clone.Warnings = Warnings != null ? new List<Engine.Info>(Warnings) : null;
        clone.Signature = Signature;
        clone.Context = Context;
        return clone;
    }
}
