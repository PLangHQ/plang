using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules.file;

public record move
{
    public virtual string source { get; init; } = null!;
    public virtual string destination { get; init; } = null!;
    public virtual bool overwrite { get; init; }
}

public sealed partial class MoveHandler : BaseClass<move>
{
    protected override Task<Return> ExecuteAsync(move? p)
    {
        if (p == null)
            return ErrorTask("Parameters required for move");

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

            FileSystem.File.Move(absSource, absDest, p.overwrite);
            return SuccessTask(new { Source = absSource, Destination = absDest });
        }
        catch (Exception ex)
        {
            return ErrorTask($"Failed to move file: {ex.Message}");
        }
    }
}
