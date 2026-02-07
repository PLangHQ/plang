using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record copy
{
    public virtual string source { get; init; } = null!;
    public virtual string destination { get; init; } = null!;
    public virtual bool overwrite { get; init; }
}

public sealed partial class CopyHandler : BaseClass<copy>
{
    protected override Task<Return> ExecuteAsync(copy? p)
    {
        if (p == null)
            return ErrorTask("Parameters required for copy");

        if (string.IsNullOrEmpty(p.source))
            return ErrorTask("Source path is required");
        if (string.IsNullOrEmpty(p.destination))
            return ErrorTask("Destination path is required");

        try
        {
            var absSource = FileSystem.Path.GetFullPath(p.source);
            var absDest = FileSystem.Path.GetFullPath(p.destination);

            var destDir = FileSystem.Path.GetDirectoryName(absDest);
            if (!string.IsNullOrEmpty(destDir) && !FileSystem.Directory.Exists(destDir))
            {
                FileSystem.Directory.CreateDirectory(destDir);
            }

            FileSystem.File.Copy(absSource, absDest, p.overwrite);
            return SuccessTask(new { Source = absSource, Destination = absDest });
        }
        catch (Exception ex)
        {
            return ErrorTask($"Failed to copy file: {ex.Message}");
        }
    }
}
