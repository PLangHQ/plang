using PLang.Runtime2.Memory;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.modules.file;

[Action("save")]
public partial class Save : IContext
{
    public partial string Path { get; init; }
    public partial object Value { get; init; }

    public async Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);
        var dir = fs.Path.GetDirectoryName(absPath);

        if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
            fs.Directory.CreateDirectory(dir);

        if (Value is byte[] bytes)
        {
            await fs.File.WriteAllBytesAsync(absPath, bytes);
        }
        else if (Value is string str)
        {
            await fs.File.WriteAllTextAsync(absPath, str);
        }
        else
        {
            var ext = fs.Path.GetExtension(absPath);
            await using var stream = fs.File.Create(absPath);
            await Context.Engine.Serializers.SerializeAsync(new SerializeOptions
                { Stream = stream, Data = Value, Extension = ext });
        }

        return Data.Ok(new types.@file(absPath, fs));
    }
}
