using PLang.Interfaces;
using PLang.Runtime2.Engine.Channels.Serializers;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.FileSystem;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.modules.file.providers;

public class DefaultFileProvider : IFileProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data Read(Read action)
    {
        var fs = action.Context.Engine.FileSystem;
        var path = action.Path;
        if (!fs.File.Exists(path.Absolute))
            return Data.FromError(new ServiceError($"File not found: {path.Raw}", "NotFound", 404));

        try
        {
            var mime = TypeMapping.GetMimeType(path.Extension);
            var type = Engine.Memory.Type.FromMime(mime);
            object content = type.ClrType == typeof(byte[])
                ? fs.File.ReadAllBytes(path.Absolute)
                : fs.File.ReadAllText(path.Absolute);

            return new PathData(path.Absolute, fs, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public async Task<Data> Save(Save action)
    {
        var fs = action.Context.Engine.FileSystem;
        var path = action.Path;
        try
        {
            EnsureDirectory(fs, fs.Path.GetDirectoryName(path.Absolute));

            var value = action.Value?.Value;
            if (value is byte[] bytes)
                await fs.File.WriteAllBytesAsync(path.Absolute, bytes);
            else if (value is string str)
                await fs.File.WriteAllTextAsync(path.Absolute, str);
            else
            {
                await using var stream = fs.File.Create(path.Absolute);
                await action.Context.Engine.Channels.Serializers.SerializeAsync(new SerializeOptions
                    { Stream = stream, Data = value, Extension = path.Extension });
            }

            return new PathData(path.Absolute, fs);
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
        var fs = action.Context.Engine.FileSystem;
        var path = action.Path;
        try
        {
            if (fs.File.Exists(path.Absolute))
                fs.File.Delete(path.Absolute);
            else if (fs.Directory.Exists(path.Absolute))
            {
                if (!action.Recursive && fs.Directory.GetFileSystemEntries(path.Absolute).Length > 0)
                    return Data.FromError(new ServiceError(
                        $"Directory is not empty: {path.Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));

                fs.Directory.Delete(path.Absolute, action.Recursive);
            }
            else if (!action.IgnoreIfNotFound)
                return Data.FromError(new ServiceError($"Not found: {path.Raw}", "NotFound", 404));

            return new PathData(path.Absolute, fs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Copy(Copy action)
    {
        var fs = action.Context.Engine.FileSystem;
        var source = action.Source;
        if (!source.Exists)
            return Data.FromError(new ServiceError($"Not found: {source.Raw}", "NotFound", 404));

        try
        {
            var destPath = ResolveDestinationPath(fs, source, action.Destination);
            EnsureDirectory(fs, fs.Path.GetDirectoryName(destPath));

            if (fs.File.Exists(source.Absolute))
                fs.File.Copy(source.Absolute, destPath, action.Overwrite);
            else
                CopyDirectory(fs, source.Absolute, destPath, action.Overwrite, action.IncludeSubfolders);

            return new PathData(destPath, fs, source: source.Absolute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Move(Move action)
    {
        var fs = action.Context.Engine.FileSystem;
        var source = action.Source;
        if (!source.Exists)
            return Data.FromError(new ServiceError($"Not found: {source.Raw}", "NotFound", 404));

        try
        {
            var destPath = ResolveDestinationPath(fs, source, action.Destination);
            EnsureDirectory(fs, fs.Path.GetDirectoryName(destPath));

            if (fs.File.Exists(source.Absolute))
                fs.File.Move(source.Absolute, destPath, action.Overwrite);
            else
            {
                if (action.Overwrite && fs.Directory.Exists(destPath))
                    fs.Directory.Delete(destPath, recursive: true);

                fs.Directory.Move(source.Absolute, destPath);
            }

            return new PathData(destPath, fs, source: source.Absolute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data List(List action)
    {
        var fs = action.Context.Engine.FileSystem;
        var path = action.Path;
        if (!fs.Directory.Exists(path.Absolute))
            return Data.FromError(new ServiceError($"Directory not found: {path.Raw}", "NotFound", 404));

        try
        {
            var option = action.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = fs.Directory.GetFiles(path.Absolute, action.Pattern, option)
                .Select(f => new PathData(f, fs))
                .ToArray();
            return Data.Ok(files);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public PathData Exists(Exists action)
    {
        return new PathData(action.Path.Absolute, action.Context.Engine.FileSystem);
    }

    // --- Helpers ---

    private static string ResolveDestinationPath(IPLangFileSystem fs, PLangPath source, PLangPath destination)
    {
        if (fs.File.Exists(source.Absolute) && fs.Directory.Exists(destination.Absolute))
            return fs.Path.Combine(destination.Absolute, source.FileName);
        return destination.Absolute;
    }

    private static void EnsureDirectory(IPLangFileSystem fs, string? dir)
    {
        if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
            fs.Directory.CreateDirectory(dir);
    }

    private static void CopyDirectory(IPLangFileSystem fs, string src, string dest, bool overwrite, bool includeSubfolders)
    {
        fs.Directory.CreateDirectory(dest);

        foreach (var file in fs.Directory.GetFiles(src))
        {
            var fileName = fs.Path.GetFileName(file);
            fs.File.Copy(file, fs.Path.Combine(dest, fileName), overwrite);
        }

        if (!includeSubfolders) return;

        foreach (var subDir in fs.Directory.GetDirectories(src))
        {
            var dirName = fs.Path.GetFileName(subDir);
            CopyDirectory(fs, subDir, fs.Path.Combine(dest, dirName), overwrite, includeSubfolders);
        }
    }
}
