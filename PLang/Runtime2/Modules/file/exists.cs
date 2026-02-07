using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record exists
{
    public virtual string path { get; init; } = null!;
}

public sealed partial class ExistsHandler : BaseClass<exists>
{
    protected override Task<Return> ExecuteAsync(exists? p)
    {
        if (p == null || string.IsNullOrEmpty(p.path))
            return ErrorTask("Path is required");

        var absPath = FileSystem.Path.GetFullPath(p.path);
        return SuccessTask(FileSystem.File.Exists(absPath));
    }
}
