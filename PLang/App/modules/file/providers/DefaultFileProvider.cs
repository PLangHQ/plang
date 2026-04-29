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

    public Data.@this Read(Read action)
    {
        var fs = action.Context.App.FileSystem;
        var path = action.Path.Value!;
        // During build: use snapshotted .pr content to avoid reading overwritten files
        if (action.Context.App.Build.IsEnabled && path.Extension == ".pr")
        {
            var snapshot = action.Context.App.Build.GetPrSnapshot(path.Absolute);
            if (snapshot != null)
            {
                var snapshotType = Data.Type.FromMime(TypeMapping.GetMimeType(path.Extension));
                var snapshotClr = snapshotType.ClrType;
                if (snapshotClr != null && snapshotClr != typeof(string))
                {
                    var (converted, _) = TypeMapping.TryConvertTo(snapshot, snapshotClr);
                    if (converted != null)
                        return new App.Data.@this(path.Raw, converted, snapshotType);
                }
                return new App.Data.@this(path.Raw, snapshot, snapshotType);
            }
        }

        if (!fs.File.Exists(path.Absolute))
            return App.Data.@this.FromError(new ServiceError($"File not found: {path.Raw}", "NotFound", 404));

        try
        {
            var mime = TypeMapping.GetMimeType(path.Extension);
            var type = Data.Type.FromMime(mime);
            object content;

            if (type.ClrType == typeof(byte[]))
            {
                content = fs.File.ReadAllBytes(path.Absolute);
            }
            else
            {
                var text = fs.File.ReadAllText(path.Absolute);

                // During build: snapshot .pr file content on first read
                if (action.Context.App.Build.IsEnabled && path.Extension == ".pr")
                    action.Context.App.Build.SnapshotPrFile(path.Absolute, text);

                var clr = type.ClrType;
                // If the type has a CLR mapping (not just string), deserialize
                if (clr != null && clr != typeof(string))
                {
                    var (converted, convertError) = TypeMapping.TryConvertTo(text, clr);
                    content = converted ?? text;
                }
                else
                {
                    content = text;
                }
            }

            return new App.Data.@this(path.Raw, content, Data.Type.FromMime(mime));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public async Task<Data.@this> Save(Save action)
    {
        var fs = action.Context.App.FileSystem;
        var path = action.Path.Value!;
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

            var resultPath = new FileSystem.Path(path.Absolute, action.Context);
            return Data.@this<FileSystem.Path>.Ok(resultPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "SerializationError", 500));
        }
    }

    public Data.@this Delete(Delete action)
    {
        var fs = action.Context.App.FileSystem;
        var path = action.Path.Value!;
        try
        {
            if (fs.File.Exists(path.Absolute))
                fs.File.Delete(path.Absolute);
            else if (fs.Directory.Exists(path.Absolute))
            {
                if (!action.Recursive.Value && fs.Directory.GetFileSystemEntries(path.Absolute).Length > 0)
                    return App.Data.@this.FromError(new ServiceError(
                        $"Directory is not empty: {path.Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));

                fs.Directory.Delete(path.Absolute, action.Recursive.Value);
            }
            else if (!action.IgnoreIfNotFound.Value)
                return App.Data.@this.FromError(new ServiceError($"Not found: {path.Raw}", "NotFound", 404));

            var resultPath = new FileSystem.Path(path.Absolute, action.Context);
            return Data.@this<FileSystem.Path>.Ok(resultPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data.@this Copy(Copy action)
    {
        var fs = action.Context.App.FileSystem;
        var source = action.Source.Value!;
        if (!source.Exists)
            return App.Data.@this.FromError(new ServiceError($"Not found: {source.Raw}", "NotFound", 404));

        try
        {
            var destination = action.Destination.Value!;
            var destPath = ResolveDestinationPath(fs, source, destination);
            EnsureDirectory(fs, fs.Path.GetDirectoryName(destPath));

            if (fs.File.Exists(source.Absolute))
                fs.File.Copy(source.Absolute, destPath, action.Overwrite.Value);
            else
                CopyDirectory(fs, source.Absolute, destPath, action.Overwrite.Value, action.IncludeSubfolders.Value);

            var resultPath = new FileSystem.Path(destPath, action.Context, source: source.Absolute);
            return Data.@this<FileSystem.Path>.Ok(resultPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data.@this Move(Move action)
    {
        var fs = action.Context.App.FileSystem;
        var source = action.Source.Value!;
        if (!source.Exists)
            return App.Data.@this.FromError(new ServiceError($"Not found: {source.Raw}", "NotFound", 404));

        try
        {
            var destination = action.Destination.Value!;
            var destPath = ResolveDestinationPath(fs, source, destination);
            EnsureDirectory(fs, fs.Path.GetDirectoryName(destPath));

            if (fs.File.Exists(source.Absolute))
                fs.File.Move(source.Absolute, destPath, action.Overwrite.Value);
            else
            {
                if (action.Overwrite.Value && fs.Directory.Exists(destPath))
                    fs.Directory.Delete(destPath, recursive: true);

                fs.Directory.Move(source.Absolute, destPath);
            }

            var resultPath = new FileSystem.Path(destPath, action.Context, source: source.Absolute);
            return Data.@this<FileSystem.Path>.Ok(resultPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data.@this List(List action)
    {
        var fs = action.Context.App.FileSystem;
        var path = action.Path.Value!;
        if (!fs.Directory.Exists(path.Absolute))
            return App.Data.@this.FromError(new ServiceError($"Directory not found: {path.Raw}", "NotFound", 404));

        try
        {
            var option = action.Recursive.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = fs.Directory.GetFiles(path.Absolute, action.Pattern.Value!, option)
                .Select(f => new FileSystem.Path(f, action.Context))
                .ToArray();
            return App.Data.@this.Ok(files);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return App.Data.@this.FromError(new ServiceError(ex.Message, "IOError", 500));
        }
    }

    public Data.@this Exists(Exists action)
    {
        var path = action.Path.Value!;
        var result = new FileSystem.Path(path.Absolute, action.Context);
        return Data.@this<FileSystem.Path>.Ok(result);
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
