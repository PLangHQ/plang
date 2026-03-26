using PLang.Interfaces;
using PLang.Runtime2.Engine.Channels.Serializers;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.file.providers;

public class DefaultFileProvider : IFileProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private readonly IPLangFileSystem _fs;

    public DefaultFileProvider(IPLangFileSystem fs)
    {
        _fs = fs;
    }

    public Data Read(Read action)
    {
        var path = action.Path;
        if (!_fs.File.Exists(path.Absolute))
            return Data.FromError(new ServiceError($"File not found: {path.Raw}", "NotFound", 404));

        try
        {
            var file = new types.@file(path.Absolute, _fs);
            _ = file.Value; // Eager-read so step cache captures content
            return Data.Ok(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public async Task<Data> Save(Save action)
    {
        var path = action.Path;
        try
        {
            EnsureDirectory(_fs.Path.GetDirectoryName(path.Absolute));

            var value = action.Value?.Value;
            if (value is byte[] bytes)
                await _fs.File.WriteAllBytesAsync(path.Absolute, bytes);
            else if (value is string str)
                await _fs.File.WriteAllTextAsync(path.Absolute, str);
            else
            {
                await using var stream = _fs.File.Create(path.Absolute);
                await action.Context.Engine.Channels.Serializers.SerializeAsync(new SerializeOptions
                    { Stream = stream, Data = value, Extension = path.Extension });
            }

            return Data.Ok(new types.@file(path.Absolute, _fs));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return Data.FromError(new ServiceError(ex.Message, "SerializationError", 500));
        }
    }

    public Data Delete(Delete action)
    {
        var path = action.Path;
        try
        {
            if (_fs.File.Exists(path.Absolute))
                _fs.File.Delete(path.Absolute);
            else if (_fs.Directory.Exists(path.Absolute))
            {
                if (!action.Recursive && _fs.Directory.GetFileSystemEntries(path.Absolute).Length > 0)
                    return Data.FromError(new ServiceError(
                        $"Directory is not empty: {path.Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));

                _fs.Directory.Delete(path.Absolute, action.Recursive);
            }
            else if (!action.IgnoreIfNotFound)
                return Data.FromError(new ServiceError($"Not found: {path.Raw}", "NotFound", 404));

            return Data.Ok(new types.@file(path.Absolute, _fs));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Copy(Copy action)
    {
        var source = action.Source;
        if (!source.Exists)
            return Data.FromError(new ServiceError($"Not found: {source.Raw}", "NotFound", 404));

        try
        {
            var destPath = ResolveDestinationPath(source, action.Destination);
            EnsureDirectory(_fs.Path.GetDirectoryName(destPath));

            if (_fs.File.Exists(source.Absolute))
                _fs.File.Copy(source.Absolute, destPath, action.Overwrite);
            else
                CopyDirectory(source.Absolute, destPath, action.Overwrite, action.IncludeSubfolders);

            return Data.Ok(new types.@file(destPath, _fs, source: source.Absolute));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Move(Move action)
    {
        var source = action.Source;
        if (!source.Exists)
            return Data.FromError(new ServiceError($"Not found: {source.Raw}", "NotFound", 404));

        try
        {
            var destPath = ResolveDestinationPath(source, action.Destination);
            EnsureDirectory(_fs.Path.GetDirectoryName(destPath));

            if (_fs.File.Exists(source.Absolute))
                _fs.File.Move(source.Absolute, destPath, action.Overwrite);
            else
            {
                if (action.Overwrite && _fs.Directory.Exists(destPath))
                    _fs.Directory.Delete(destPath, recursive: true);

                _fs.Directory.Move(source.Absolute, destPath);
            }

            return Data.Ok(new types.@file(destPath, _fs, source: source.Absolute));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data List(List action)
    {
        var path = action.Path;
        if (!_fs.Directory.Exists(path.Absolute))
            return Data.FromError(new ServiceError($"Directory not found: {path.Raw}", "NotFound", 404));

        try
        {
            var option = action.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = _fs.Directory.GetFiles(path.Absolute, action.Pattern, option)
                .Select(f => new types.@file(f, _fs))
                .ToArray();
            return Data.Ok(files);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Exists(Exists action)
    {
        return Data.Ok(new types.@file(action.Path.Absolute, _fs));
    }

    // --- Helpers ---

    private string ResolveDestinationPath(PLangPath source, PLangPath destination)
    {
        if (_fs.File.Exists(source.Absolute) && _fs.Directory.Exists(destination.Absolute))
            return _fs.Path.Combine(destination.Absolute, source.FileName);
        return destination.Absolute;
    }

    private void EnsureDirectory(string? dir)
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
}
