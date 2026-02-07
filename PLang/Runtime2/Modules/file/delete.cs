using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record delete
{
    public virtual string path { get; init; } = null!;
}

public sealed partial class DeleteHandler : BaseClass<delete>
{
    protected override Task<Return> ExecuteAsync(delete? p)
    {
        if (p == null || string.IsNullOrEmpty(p.path))
            return ErrorTask("Path is required");

        try
        {
            var absPath = FileSystem.Path.GetFullPath(p.path);

            if (FileSystem.File.Exists(absPath))
            {
                FileSystem.File.Delete(absPath);
            }

            return SuccessTask();
        }
        catch (Exception ex)
        {
            return ErrorTask($"Failed to delete file: {ex.Message}");
        }
    }
}
