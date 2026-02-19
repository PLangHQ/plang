using PLang.Interfaces;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Rich path wrapper for PLang action parameters.
/// Resolves raw path strings into absolute paths with navigable properties.
/// The source generator detects Resolve(string, Engine) and auto-wraps string parameters.
/// </summary>
public sealed class Path
{
    private readonly IPLangFileSystem _fs;
    private readonly string _rawPath;
    private readonly string _absolutePath;

    // Cached string-derived properties (never change once path is set)
    private string? _extension;
    private string? _fileName;
    private string? _fileNameWithoutExtension;
    private string? _directory;

    public Path(string rawPath, IPLangFileSystem fileSystem)
    {
        _rawPath = rawPath;
        _fs = fileSystem;
        _absolutePath = fileSystem.Path.GetFullPath(rawPath);
    }

    /// <summary>
    /// Engine-resolvable convention — source generator detects this static method
    /// and generates: Path.Resolve(__Resolve&lt;string&gt;("path"), __engine!)
    /// </summary>
    public static Path Resolve(string rawPath, Engine.@this engine)
        => new Path(rawPath, engine.FileSystem);

    /// <summary>The raw path string as provided (before resolution)</summary>
    public string Raw => _rawPath;

    /// <summary>Absolute path on disk</summary>
    public string Absolute => _absolutePath;

    /// <summary>Path relative to the engine's root directory</summary>
    public string Relative
    {
        get
        {
            if (_absolutePath.StartsWith(_fs.RootDirectory, StringComparison.OrdinalIgnoreCase))
            {
                var rel = _absolutePath[_fs.RootDirectory.Length..];
                return rel.TrimStart(_fs.Path.DirectorySeparatorChar, _fs.Path.AltDirectorySeparatorChar);
            }
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
    public string MimeType => TypeMapping.GetMimeType(Extension);

    /// <summary>Whether a file exists at this path (live check, not cached)</summary>
    public bool IsFile => _fs.File.Exists(_absolutePath);

    /// <summary>Whether a directory exists at this path (live check, not cached)</summary>
    public bool IsDirectory => _fs.Directory.Exists(_absolutePath);

    /// <summary>Whether anything exists at this path (live check, not cached)</summary>
    public bool Exists => IsFile || IsDirectory;

    /// <summary>File size in bytes. Returns 0 if file doesn't exist. (live check)</summary>
    public long Size
    {
        get
        {
            var info = _fs.FileInfo.New(_absolutePath);
            return info.Exists ? info.Length : 0;
        }
    }

    /// <summary>Returns absolute path — allows Path to be used where string is expected</summary>
    public override string ToString() => _absolutePath;

    public override bool Equals(object? obj) => obj switch
    {
        Path other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
        string str => string.Equals(_absolutePath, str, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);
}
