using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.file;

public record copy
{
    public virtual string source { get; init; } = null!;
    public virtual string destination { get; init; } = null!;
    public virtual bool overwrite { get; init; }
}

public sealed partial class CopyHandler : BaseClass<copy>
{
    protected override Task<Data> ExecuteAsync(copy p)
    {
        var absSource = FileSystem.Path.GetFullPath(p.source);
        var absDest = FileSystem.Path.GetFullPath(p.destination);

        var destDir = FileSystem.Path.GetDirectoryName(absDest);
        if (!string.IsNullOrEmpty(destDir) && !FileSystem.Directory.Exists(destDir))
        {
            FileSystem.Directory.CreateDirectory(destDir);
        }

        FileSystem.File.Copy(absSource, absDest, p.overwrite);
        return SuccessTask(new types.@file(absDest, FileSystem, source: absSource));
    }
}
