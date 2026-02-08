using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.file;

public record read
{
    public virtual string path { get; init; } = null!;
}

public sealed partial class ReadHandler : BaseClass<read>
{
    protected override Task<Data> ExecuteAsync(read p)
    {
        var absPath = FileSystem.Path.GetFullPath(p.path);

        if (!FileSystem.File.Exists(absPath))
            return ErrorTask($"File not found: {p.path}");

        return SuccessTask(new types.@file(absPath, FileSystem));
    }
}
