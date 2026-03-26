using PLang.Interfaces;
using PLangContext = PLang.Runtime2.Engine.Context.PLangContext;

namespace PLang.Runtime2.Engine.FileSystem;

/// <summary>
/// Rich path wrapper for PLang action parameters.
/// Resolves raw path strings into absolute paths with navigable properties.
/// Relative paths resolve against the goal's folder, not the engine root.
/// The source generator detects Resolve(string, PLangContext) and auto-wraps string parameters.
/// </summary>
public sealed class Path
{
    private readonly Engine.@this _engine;
    private readonly IPLangFileSystem _fs;
    private readonly string _rawPath;
    private readonly string _absolutePath;

    // Cached string-derived properties (never change once path is set)
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;

    /// <summary>
    /// Internal constructor for absolute paths (e.g., ResolveDestination).
    /// No goal-relative resolution needed — path is already resolved.
    /// </summary>
    private Path(string absolutePath, Engine.@this engine)
    {
        _rawPath = absolutePath;
        _engine = engine;
        _fs = engine.FileSystem;
        _absolutePath = _fs.ValidatePath(absolutePath);
    }

    public Path(string rawPath, PLangContext context)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        ArgumentNullException.ThrowIfNull(context);

        _rawPath = rawPath;
        _engine = context.Engine;
        _fs = _engine.FileSystem;

        // Relative paths resolve against the goal's folder
        if (!rawPath.StartsWith('/') && !rawPath.StartsWith('\\') && !rawPath.Contains("://"))
        {
            var goalPath = context.Goal?.Path;
            if (!string.IsNullOrEmpty(goalPath))
            {
                var goalDir = _fs.Path.GetDirectoryName(goalPath);
                if (!string.IsNullOrEmpty(goalDir))
                    rawPath = _fs.Path.Combine(goalDir, rawPath);
            }
        }

        _absolutePath = _fs.ValidatePath(rawPath);
    }

    /// <summary>
    /// Context-resolvable convention — source generator detects this static method
    /// and generates: Path.Resolve(__Resolve&lt;string&gt;("path"), Context)
    /// </summary>
    public static Path Resolve(string rawPath, PLangContext context)
        => new Path(rawPath, context);

    /// <summary>The raw path string as provided (before resolution)</summary>
    public string Raw => _rawPath;

    /// <summary>Absolute path on disk</summary>
    public string Absolute => _absolutePath;

    /// <summary>Path relative to the engine's root directory</summary>
    public string Relative
    {
        get
        {
            var root = _fs.RootDirectory;

            // Ensure comparison includes trailing separator to avoid
            // prefix false positives ("/app" matching "/application")
            if (!root.EndsWith(_fs.Path.DirectorySeparatorChar) && !root.EndsWith(_fs.Path.AltDirectorySeparatorChar))
                root += _fs.Path.DirectorySeparatorChar;

            if (_absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return _absolutePath[root.Length..];

            // Exact match (path IS the root directory)
            if (string.Equals(_absolutePath, _fs.RootDirectory, StringComparison.OrdinalIgnoreCase))
                return ".";

            return _absolutePath;
        }
    }

    /// <summary>File extension including the dot (e.g., ".json")</summary>
    public string Extension => _extension ??= _fs.Path.GetExtension(_absolutePath);

    /// <summary>File name with extension (e.g., "config.json")</summary>
    public string FileName => _fileName ??= _fs.Path.GetFileName(_absolutePath);

    /// <summary>File name without extension (e.g., "config")</summary>
    public string FileNameWithoutExtension
        => _fileNameWithoutExtension ??= _fs.Path.GetFileNameWithoutExtension(_absolutePath);

    /// <summary>Parent directory path</summary>
    public string Directory => _directory ??= _fs.Path.GetDirectoryName(_absolutePath) ?? _absolutePath;

    /// <summary>MIME type derived from file extension</summary>
    public string MimeType => _engine.Types.Mime(Extension);

    /// <summary>Structural: has a file extension (no I/O)</summary>
    public bool IsFile => !string.IsNullOrEmpty(Extension);

    /// <summary>Structural: no file extension (no I/O)</summary>
    public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <summary>Whether anything exists at this path (live filesystem check)</summary>
    public bool Exists => _fs.File.Exists(_absolutePath) || _fs.Directory.Exists(_absolutePath);

    /// <summary>File size in bytes. Returns 0 if file doesn't exist. (live check)</summary>
    public long Size
    {
        get
        {
            var info = _fs.FileInfo.New(_absolutePath);
            return info.Exists ? info.Length : 0;
        }
    }

    /// <summary>Returns relative path — PLang users think in relative paths</summary>
    public override string ToString() => Relative;

    public override bool Equals(object? obj) => obj switch
    {
        Path other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
        string str => string.Equals(_absolutePath, str, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);
}
