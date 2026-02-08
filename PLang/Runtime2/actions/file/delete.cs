using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.file;

public record delete
{
    public virtual string path { get; init; } = null!;
}

public sealed partial class DeleteHandler : BaseClass<delete>
{
    protected override Task<Data> ExecuteAsync(delete p)
    {
        var absPath = FileSystem.Path.GetFullPath(p.path);

        if (FileSystem.File.Exists(absPath))
        {
            FileSystem.File.Delete(absPath);
        }

        return SuccessTask(new types.@file(absPath, FileSystem));
    }
}
