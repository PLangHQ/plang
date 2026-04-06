using App.FileSystem;
using App.FileSystem.Default;
using App.Channels.Serializers;
using App.Errors;
using App.FileSystem;
using App.Variables;
using App.Utils;

namespace App.modules.file.providers;

public class DefaultFileProvider : IFileProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data Read(Read action)
    {
        var fs = action.Context.App.FileSystem;
        var path = action.Path;
        if (!fs.File.Exists(path.Absolute))
            return Data.FromError(new ServiceError($"File not found: {path.Raw}", "NotFound", 404));

        try
        {
            var mime = TypeMapping.GetMimeType(path.Extension);
            var type = Variables.Type.FromMime(mime);
            object content;

            if (type.ClrType == typeof(byte[]))
            {
                content = fs.File.ReadAllBytes(path.Absolute);
            }
            else
            {
                var text = fs.File.ReadAllText(path.Absolute);
                var clr = type.ClrType;
                // If the type has a CLR mapping (not just string), deserialize
                if (clr != null && clr != typeof(string))
                {
                    var (converted, convertError) = TypeMapping.TryConvertTo(text, clr);
                    content = converted ?? text;

                    // Set back-references for .pr deserialization (temporary — will be replaced by Variables provenance)
                    if (content is Goals.Goal.@this prGoal)
                        prGoal.SetStepBackReferences();
                }
                else
                {
                    content = text;
                }
            }

            return new FileSystem.Path(path.Absolute, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public async Task<Data> Save(Save action)
    {
        var fs = action.Context.App.FileSystem;
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
                await action.Context.App.Channels.Serializers.SerializeAsync(new SerializeOptions
                    { Stream = stream, Data = value, Extension = path.Extension });
            }

            return new FileSystem.Path(path.Absolute);
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
        var fs = action.Context.App.FileSystem;
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

            return new FileSystem.Path(path.Absolute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Copy(Copy action)
    {
        var fs = action.Context.App.FileSystem;
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

            return new FileSystem.Path(destPath, source: source.Absolute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data Move(Move action)
    {
        var fs = action.Context.App.FileSystem;
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

            return new FileSystem.Path(destPath, source: source.Absolute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data List(List action)
    {
        var fs = action.Context.App.FileSystem;
        var path = action.Path;
        if (!fs.Directory.Exists(path.Absolute))
            return Data.FromError(new ServiceError($"Directory not found: {path.Raw}", "NotFound", 404));

        try
        {
            var option = action.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = fs.Directory.GetFiles(path.Absolute, action.Pattern, option)
                .Select(f => new FileSystem.Path(f))
                .ToArray();
            return Data.Ok(files);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public FileSystem.Path Exists(Exists action)
    {
        return new FileSystem.Path(action.Path.Absolute);
    }

    // --- Helpers ---

    private static string ResolveDestinationPath(IPLangFileSystem fs, FileSystem.Path source, FileSystem.Path destination)
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
