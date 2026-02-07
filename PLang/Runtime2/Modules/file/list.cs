using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record list
{
    public virtual string path { get; init; } = null!;
    public virtual string pattern { get; init; } = "*";
    public virtual bool recursive { get; init; } = false;
}

public sealed partial class ListHandler : BaseClass<list>
{
    protected override Task<Return> ExecuteAsync(list? p)
    {
        if (p == null || string.IsNullOrEmpty(p.path))
            return ErrorTask("Path is required");

        try
        {
            var absPath = FileSystem.Path.GetFullPath(p.path);

            if (!FileSystem.Directory.Exists(absPath))
                return ErrorTask($"Directory not found: {p.path}");

            var searchOption = p.recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = FileSystem.Directory.GetFiles(absPath, p.pattern, searchOption);
            return SuccessTask(files);
        }
        catch (Exception ex)
        {
            return ErrorTask($"Failed to list files: {ex.Message}");
        }
    }
}
