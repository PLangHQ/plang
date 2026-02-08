using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.file;

public record list
{
    public virtual string path { get; init; } = null!;
    public virtual string pattern { get; init; } = "*";
    public virtual bool recursive { get; init; } = false;
}

public sealed partial class ListHandler : BaseClass<list>
{
    protected override Task<Data> ExecuteAsync(list p)
    {
        var absPath = FileSystem.Path.GetFullPath(p.path);

        if (!FileSystem.Directory.Exists(absPath))
            return ErrorTask($"Directory not found: {p.path}");

        return SuccessTask(new types.@file(absPath, FileSystem));
    }
}
