using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions.file;

public record move
{
    public virtual string source { get; init; } = null!;
    public virtual string destination { get; init; } = null!;
    public virtual bool overwrite { get; init; }
}

public sealed partial class MoveHandler : BaseClass<move>
{
    protected override Task<Data> ExecuteAsync(move p)
    {
        var absSource = FileSystem.Path.GetFullPath(p.source);
        var absDest = FileSystem.Path.GetFullPath(p.destination);

        var destDir = FileSystem.Path.GetDirectoryName(absDest);
        if (!string.IsNullOrEmpty(destDir) && !FileSystem.Directory.Exists(destDir))
        {
            FileSystem.Directory.CreateDirectory(destDir);
        }
		// fix: and if file doesn't exists?? we should return error key:FileNotFound
        FileSystem.File.Move(absSource, absDest, p.overwrite);
        return SuccessTask(new types.@file(absDest, FileSystem, source: absSource));
    }
}
