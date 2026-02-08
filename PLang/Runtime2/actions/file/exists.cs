using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.file;

public record exists
{
    public virtual string path { get; init; } = null!;
}

public sealed partial class ExistsHandler : BaseClass<exists>
{
    protected override Task<Data> ExecuteAsync(exists p)
    {
        var absPath = FileSystem.Path.GetFullPath(p.path);
        return SuccessTask(new types.@file(absPath, FileSystem));
    }
}
