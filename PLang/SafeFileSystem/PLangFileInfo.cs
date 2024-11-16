using System.IO.Abstractions;
using PLang.Interfaces;

namespace PLang.SafeFileSystem;

[Serializable]
public sealed class PLangFileInfoFactory : IFileInfoFactory
{
    private readonly IPLangFileSystem fileSystem;

    /// <inheritdoc />
    public PLangFileInfoFactory(IPLangFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public IFileSystem FileSystem
        => fileSystem;

    /// <inheritdoc />
    public IFileInfo New(string fileName)
    {
        fileName = fileSystem.ValidatePath(fileName);

        var realFileInfo = new FileInfo(fileName);
        return new FileInfoWrapper(fileSystem, realFileInfo);
    }

    /// <inheritdoc />
    public IFileInfo? Wrap(FileInfo? fileInfo)
    {
        if (fileInfo == null) return null;

        return new FileInfoWrapper(fileSystem, fileInfo);
    }

    /// <inheritdoc />
    [Obsolete("Use `IFileInfoFactory.New(string)` instead")]
    public IFileInfo FromFileName(string fileName)
    {
        return New(fileName);
    }
}