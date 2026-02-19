using PLang.Interfaces;
using PLang.Runtime2.Engine.Channels.Serializers;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Rich path wrapper for PLang action parameters.
/// Resolves raw path strings into absolute paths with navigable properties.
/// The source generator detects Resolve(string, Engine) and auto-wraps string parameters.
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

    public Path(string rawPath, Engine.@this engine)
    {
        _rawPath = rawPath;
        _engine = engine;
        _fs = engine.FileSystem;
        _absolutePath = _fs.Path.GetFullPath(rawPath);
    }

    /// <summary>
    /// Engine-resolvable convention — source generator detects this static method
    /// and generates: Path.Resolve(__Resolve&lt;string&gt;("path"), __engine!)
    /// </summary>
    public static Path Resolve(string rawPath, Engine.@this engine)
        => new Path(rawPath, engine);

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

    // --- Behavior methods (OBP: behavior belongs to the owner) ---

    public Data Copy(actions.file.Copy action)
    {
        if (!Exists)
            return Data.FromError(new ServiceError($"Not found: {Raw}", "NotFound", 404));

        EnsureDirectory(action.Destination.Directory);

        if (_fs.File.Exists(_absolutePath))
            _fs.File.Copy(_absolutePath, action.Destination.Absolute, action.Overwrite);
        else
            CopyDirectory(_absolutePath, action.Destination.Absolute, action.Overwrite, action.IncludeSubfolders);

        return Data.Ok(new actions.file.types.@file(action.Destination.Absolute, _fs, source: _absolutePath));
    }

    public Data Move(actions.file.Move action)
    {
        if (!Exists)
            return Data.FromError(new ServiceError($"Not found: {Raw}", "NotFound", 404));

        EnsureDirectory(action.Destination.Directory);

        if (_fs.File.Exists(_absolutePath))
            _fs.File.Move(_absolutePath, action.Destination.Absolute, action.Overwrite);
        else
            _fs.Directory.Move(_absolutePath, action.Destination.Absolute);

        return Data.Ok(new actions.file.types.@file(action.Destination.Absolute, _fs, source: _absolutePath));
    }

    public Data Delete(actions.file.Delete action)
    {
        if (_fs.File.Exists(_absolutePath))
            _fs.File.Delete(_absolutePath);
        else if (_fs.Directory.Exists(_absolutePath))
            _fs.Directory.Delete(_absolutePath, action.Recursive);
        else if (!action.IgnoreIfNotFound)
            return Data.FromError(new ServiceError($"Not found: {Raw}", "NotFound", 404));

        return Data.Ok(new actions.file.types.@file(_absolutePath, _fs));
    }

    public Data Read()
    {
        if (!_fs.File.Exists(_absolutePath))
            return Data.FromError(new ServiceError($"File not found: {Raw}", "NotFound", 404));

        var file = new actions.file.types.@file(_absolutePath, _fs);
        _ = file.Value; // Eager-read so step cache captures content
        return Data.Ok(file);
    }

    public Data List(actions.file.List action)
    {
        if (!_fs.Directory.Exists(_absolutePath))
            return Data.FromError(new ServiceError($"Directory not found: {Raw}", "NotFound", 404));

        var option = action.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = _fs.Directory.GetFiles(_absolutePath, action.Pattern, option)
            .Select(f => new actions.file.types.@file(f, _fs))
            .ToArray();
        return Data.Ok(files);
    }

    /// <summary>Wraps this path as a @file object — used by exists handler</summary>
    public Data AsFile() => Data.Ok(new actions.file.types.@file(_absolutePath, _fs));

    public async Task<Data> Save(actions.file.Save action)
    {
        EnsureDirectory(Directory);

        if (action.Value is byte[] bytes)
            await _fs.File.WriteAllBytesAsync(_absolutePath, bytes);
        else if (action.Value is string str)
            await _fs.File.WriteAllTextAsync(_absolutePath, str);
        else
        {
            await using var stream = _fs.File.Create(_absolutePath);
            await _engine.Channels.Serializers.SerializeAsync(new SerializeOptions
                { Stream = stream, Data = action.Value, Extension = Extension });
        }

        return Data.Ok(new actions.file.types.@file(_absolutePath, _fs));
    }

    private void EnsureDirectory(string dir)
    {
        if (!string.IsNullOrEmpty(dir) && !_fs.Directory.Exists(dir))
            _fs.Directory.CreateDirectory(dir);
    }

    private void CopyDirectory(string src, string dest, bool overwrite, bool includeSubfolders)
    {
        _fs.Directory.CreateDirectory(dest);

        foreach (var file in _fs.Directory.GetFiles(src))
        {
            var fileName = _fs.Path.GetFileName(file);
            _fs.File.Copy(file, _fs.Path.Combine(dest, fileName), overwrite);
        }

        if (!includeSubfolders) return;

        foreach (var subDir in _fs.Directory.GetDirectories(src))
        {
            var dirName = _fs.Path.GetFileName(subDir);
            CopyDirectory(subDir, _fs.Path.Combine(dest, dirName), overwrite, includeSubfolders);
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
