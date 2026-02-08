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
		// fix: the path will not work, because copy will contain /ble/bla, 
		// this is fine if you know the fileSystem.rootPath, then we can combine
		// it and createa aboslute path. My question to llm is:
		// can we mark a virtual property like source as path with an [FilePath] attribute, and with codegen magic
		// we can have absoluteSource, then we dont need to do any GetFullPath
        var absSource = FileSystem.Path.GetFullPath(p.source);
        var absDest = FileSystem.Path.GetFullPath(p.destination);

        var destDir = FileSystem.Path.GetDirectoryName(absDest);
        if (!string.IsNullOrEmpty(destDir) && !FileSystem.Directory.Exists(destDir))
        {
            FileSystem.Directory.CreateDirectory(destDir);
        }
		// fix: we are missing file.exists check and return error, FileNotFound
        FileSystem.File.Copy(absSource, absDest, p.overwrite);
        return SuccessTask(new types.@file(absDest, FileSystem, source: absSource));
    }
}
