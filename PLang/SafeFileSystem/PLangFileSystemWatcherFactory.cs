using System.IO.Abstractions;
using PLang.Interfaces;

namespace PLang.SafeFileSystem;

[Serializable]
internal class PLangFileSystemWatcherFactory : FileSystemWatcherFactory
{
    private IPLangFileSystem fileSystem;

    ///
    public PLangFileSystemWatcherFactory(IPLangFileSystem fileSystem) : base(fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public new IFileSystem FileSystem => fileSystem;

    /// <inheritdoc />
    [Obsolete("Use `IFileSystemWatcherFactory.New()` instead")]
    public new IFileSystemWatcher CreateNew()
    {
        return New();
    }

    /// <inheritdoc />
    [Obsolete("Use `IFileSystemWatcherFactory.New(string)` instead")]
    public new IFileSystemWatcher CreateNew(string path)
    {
        return New(path);
    }

    /// <inheritdoc />
    [Obsolete("Use `IFileSystemWatcherFactory.New(string, string)` instead")]
    public new IFileSystemWatcher CreateNew(string path, string filter)
    {
        return New(path, filter);
    }

    /// <inheritdoc />
    public new IFileSystemWatcher New()
    {
        return new FileSystemWatcherWrapper(FileSystem);
    }

    /// <inheritdoc />
    public new IFileSystemWatcher New(string path)
    {
        path = fileSystem.ValidatePath(path);
        return new FileSystemWatcherWrapper(FileSystem, path);
    }

    /// <inheritdoc />
    public new IFileSystemWatcher New(string path, string filter)
    {
        path = fileSystem.ValidatePath(path);
        return new FileSystemWatcherWrapper(FileSystem, path, filter);
    }
}