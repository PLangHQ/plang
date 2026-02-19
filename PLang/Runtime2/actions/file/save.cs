using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial PLangPath Path { get; init; }
    public partial object Value { get; init; }

    public async Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var dir = Path.Directory;

        if (!string.IsNullOrEmpty(dir) && !fs.Directory.Exists(dir))
            fs.Directory.CreateDirectory(dir);

        if (Value is byte[] bytes)
        {
            await fs.File.WriteAllBytesAsync(Path.Absolute, bytes);
        }
        else if (Value is string str)
        {
            await fs.File.WriteAllTextAsync(Path.Absolute, str);
        }
        else
        {
            await using var stream = fs.File.Create(Path.Absolute);
            await Context.Engine.Channels.Serializers.SerializeAsync(new SerializeOptions
                { Stream = stream, Data = Value, Extension = Path.Extension });
        }

        return Data.Ok(new types.@file(Path.Absolute, fs));
    }
}
