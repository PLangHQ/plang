using PLang.Interfaces;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.actions.file.types;

public class @file
{
    private readonly IPLangFileSystem _fs;
    private readonly string _absolutePath;

    // Cached lazy values
    private Data? _value;
    private long? _size;

    public @file(string absolutePath, IPLangFileSystem fileSystem)
    {
        _absolutePath = absolutePath;
        _fs = fileSystem;
    }

    // For copy/move — source tracking
    public @file(string absolutePath, IPLangFileSystem fileSystem, string? source)
        : this(absolutePath, fileSystem)
    {
        Source = source;
    }

    /// <summary>Relative path from FileSystem.RootDirectory</summary>
    public string Path => _absolutePath.Replace(_fs.RootDirectory, "")
                                       .TrimStart(
                                           _fs.Path.DirectorySeparatorChar,
                                           _fs.Path.AltDirectorySeparatorChar);

    /// <summary>Absolute path on disk</summary>
    public string AbsolutePath => _absolutePath;

    /// <summary>MIME type — derived from file extension via TypeMapping</summary>
    public string Type => TypeMapping.GetMimeType(_fs.Path.GetExtension(_absolutePath));

    /// <summary>File size in bytes — uses FileInfo via IPLangFileSystem, cached</summary>
    public long Size
    {
        get
        {
            if (_size == null)
            {
                var info = _fs.FileInfo.New(_absolutePath);
                _size = info.Exists ? info.Length : 0;
            }
            return _size.Value;
        }
    }

    /// <summary>
    /// File value as Data with MIME type set.
    /// Text files -> Data.Value is string. Binary files -> Data.Value is byte[].
    /// Reads on first access, cached.
    /// </summary>
    public Data Value
    {
        get
        {
            if (_value != null) return _value;

            if (!_fs.File.Exists(_absolutePath))
            {
                _value = Data.Ok(null);
                return _value;
            }

            var mime = TypeMapping.GetMimeType(_fs.Path.GetExtension(_absolutePath));
            var type = PLang.Runtime2.Engine.Memory.Type.FromMime(mime);
            var clrType = type.ClrType;

            if (clrType == typeof(byte[]))
                _value = Data.Ok((object)_fs.File.ReadAllBytes(_absolutePath), type);
            else
                _value = Data.Ok((object)_fs.File.ReadAllText(_absolutePath), type);

            return _value;
        }
    }

    /// <summary>Whether the file exists — checked on each access (not cached)</summary>
    public bool Exists => _fs.File.Exists(_absolutePath);

    /// <summary>Source path (set by copy/move handlers)</summary>
    public string? Source { get; }

    /// <summary>Directory listing — returns lazy file objects for each child</summary>
    public @file[] Files => _fs.Directory.Exists(_absolutePath)
        ? _fs.Directory.GetFiles(_absolutePath)
              .Select(f => new @file(f, _fs))
              .ToArray()
        : Array.Empty<@file>();

    public override string ToString() => _value?.Value?.ToString() ?? Path;
}
