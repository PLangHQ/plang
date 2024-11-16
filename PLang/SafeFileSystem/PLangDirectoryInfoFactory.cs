using System.IO.Abstractions;
using PLang.Interfaces;

namespace PLang.SafeFileSystem;

[Serializable]
internal class PLangDirectoryInfoFactory : IDirectoryInfoFactory
{
    private readonly IPLangFileSystem fileSystem;

    /// <inheritdoc />
    public PLangDirectoryInfoFactory(IPLangFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public IFileSystem FileSystem
        => fileSystem;

    /// <inheritdoc />
    public IDirectoryInfo New(string path)
    {
        path = fileSystem.ValidatePath(path);
        var realDirectoryInfo = new DirectoryInfo(path);
        return new DirectoryInfoWrapper(fileSystem, realDirectoryInfo);
    }

    /// <inheritdoc />
    public IDirectoryInfo? Wrap(DirectoryInfo? directoryInfo)
    {
        if (directoryInfo == null) return null;

        return new DirectoryInfoWrapper(fileSystem, directoryInfo);
    }

    /// <inheritdoc />
    [Obsolete("Use `IDirectoryInfoFactory.New(string)` instead")]
    public IDirectoryInfo FromDirectoryName(string directoryName)
    {
        return New(directoryName);
    }
}