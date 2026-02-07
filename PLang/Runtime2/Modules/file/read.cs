using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record read
{
    public virtual string path { get; init; } = null!;
}

public sealed partial class ReadHandler : BaseClass<read>
{
    protected override async Task<Return> ExecuteAsync(read? p)
    {
        if (p == null || string.IsNullOrEmpty(p.path))
            return Error("Path is required");

        try
        {
            var absPath = FileSystem.Path.GetFullPath(p.path);

            if (!FileSystem.File.Exists(absPath))
                return Error($"File not found: {p.path}");

            var content = await FileSystem.File.ReadAllTextAsync(absPath);
            return Success(content);
        }
        catch (Exception ex)
        {
            return Error($"Failed to read file: {ex.Message}");
        }
    }
}
