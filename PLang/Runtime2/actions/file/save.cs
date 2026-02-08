using PLang.Runtime2.Memory;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.actions.file;

public record save
{
    public virtual string path { get; init; } = null!;
    public virtual object value { get; init; } = null!;
}

public sealed partial class SaveHandler : BaseClass<save>
{
    protected override async Task<Data> ExecuteAsync(save p)
    {
        var absPath = FileSystem.Path.GetFullPath(p.path);
        var dir = FileSystem.Path.GetDirectoryName(absPath);

        if (!string.IsNullOrEmpty(dir) && !FileSystem.Directory.Exists(dir))
            FileSystem.Directory.CreateDirectory(dir);

        var ext = FileSystem.Path.GetExtension(absPath);
        await using var stream = FileSystem.File.Create(absPath);

		//fix: I think this wont work, lets think about this....
		// I have so value to save, it's a file, shouldn't we just write the byte to disk, 
		// do we need any serializer??
        await Engine.Serializers.SerializeAsync(new SerializeOptions
        {
            Stream = stream, Data = p.value, Extension = ext
        });
        return Success(new types.@file(absPath, FileSystem));
    }
}
