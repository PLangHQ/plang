using System.IO.Abstractions;
using PLang.Interfaces;

namespace PLang.SafeFileSystem;

/// <inheritdoc />
[Serializable]
public class PLangDirectoryWrapper : DirectoryWrapper
{
    private IPLangFileSystem fileSystem;

    /// <inheritdoc />
    public PLangDirectoryWrapper(IPLangFileSystem fileSystem) : base(fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public override IDirectoryInfo CreateDirectory(string path)
    {
        path = fileSystem.ValidatePath(path);
        var directoryInfo = new DirectoryInfo(path);
        directoryInfo.Create();
        return new DirectoryInfoWrapper(FileSystem, directoryInfo);
    }

#if FEATURE_UNIX_FILE_MODE
        /// <inheritdoc />
        [UnsupportedOSPlatform("windows")]
        public override IDirectoryInfo CreateDirectory(string path, UnixFileMode unixCreateMode)
        {
			path = fileSystem.ValidatePath(path);
            return new DirectoryInfoWrapper(FileSystem,
                base.CreateDirectory(path, unixCreateMode));
        }
#endif

#if FEATURE_CREATE_SYMBOLIC_LINK
        /// <inheritdoc />
        public override IFileSystemInfo CreateSymbolicLink(string path, string pathToTarget)
        {
			path = fileSystem.ValidatePath(path);
            return base.CreateSymbolicLink(path, pathToTarget)
                .WrapFileSystemInfo(FileSystem);
        }
#endif

    /// <inheritdoc />
    public override void Delete(string path)
    {
        path = fileSystem.ValidatePath(path);
        base.Delete(path);
    }

    /// <inheritdoc />
    public override void Delete(string path, bool recursive)
    {
        path = fileSystem.ValidatePath(path);
        base.Delete(path, recursive);
    }

    /// <inheritdoc />
    public override bool Exists(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.Exists(path);
    }

    /// <inheritdoc />
    public override DateTime GetCreationTime(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetCreationTime(path);
    }

    /// <inheritdoc />
    public override DateTime GetCreationTimeUtc(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetCreationTimeUtc(path);
    }

    /// <inheritdoc />
    public override string GetCurrentDirectory()
    {
        return base.GetCurrentDirectory();
    }

    /// <inheritdoc />
    public override string[] GetDirectories(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetDirectories(path);
    }

    /// <inheritdoc />
    public override string[] GetDirectories(string path, string searchPattern)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetDirectories(path, searchPattern);
    }

    /// <inheritdoc />
    public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetDirectories(path, searchPattern, searchOption);
    }

#if FEATURE_ENUMERATION_OPTIONS
        /// <inheritdoc />
        public override string[] GetDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
            return base.GetDirectories(path, searchPattern, enumerationOptions);
        }
#endif

    /// <inheritdoc />
    public override string GetDirectoryRoot(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetDirectoryRoot(path);
    }

    /// <inheritdoc />
    public override string[] GetFiles(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetFiles(path);
    }

    /// <inheritdoc />
    public override string[] GetFiles(string path, string searchPattern)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetFiles(path, searchPattern);
    }

    /// <inheritdoc />
    public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetFiles(path, searchPattern, searchOption);
    }

#if FEATURE_ENUMERATION_OPTIONS
        /// <inheritdoc />
        public override string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
            return base.GetFiles(path, searchPattern, enumerationOptions);
        }
#endif

    /// <inheritdoc />
    public override string[] GetFileSystemEntries(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetFileSystemEntries(path);
    }

    /// <inheritdoc />
    public override string[] GetFileSystemEntries(string path, string searchPattern)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetFileSystemEntries(path, searchPattern);
    }

    /// <inheritdoc />
    public override string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetFileSystemEntries(path, searchPattern, searchOption);
    }

#if FEATURE_ENUMERATION_OPTIONS
        /// <inheritdoc />
        public override string[] GetFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
			path = fileSystem.ValidatePath(path);
            return base.GetFileSystemEntries(path, searchPattern, enumerationOptions);
        }
#endif

    /// <inheritdoc />
    public override DateTime GetLastAccessTime(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetLastAccessTime(path);
    }

    /// <inheritdoc />
    public override DateTime GetLastAccessTimeUtc(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetLastAccessTimeUtc(path);
    }

    /// <inheritdoc />
    public override DateTime GetLastWriteTime(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetLastWriteTime(path);
    }

    /// <inheritdoc />
    public override DateTime GetLastWriteTimeUtc(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.GetLastWriteTimeUtc(path);
    }

    /// <inheritdoc />
    public override string[] GetLogicalDrives()
    {
        fileSystem.ValidatePath("Drive names");
        return base.GetLogicalDrives();
    }

    /// <inheritdoc />
    public override IDirectoryInfo? GetParent(string path)
    {
        path = fileSystem.ValidatePath(path);
        var parent = Directory.GetParent(path);

        if (parent == null) return null;

        return new DirectoryInfoWrapper(FileSystem, parent);
    }

    /// <inheritdoc />
    public override void Move(string sourceDirName, string destDirName)
    {
        sourceDirName = fileSystem.ValidatePath(sourceDirName);
        destDirName = fileSystem.ValidatePath(destDirName);
        base.Move(sourceDirName, destDirName);
    }

#if FEATURE_CREATE_SYMBOLIC_LINK
        /// <inheritdoc />
        public override IFileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget)
        {
            return base.ResolveLinkTarget(linkPath, returnFinalTarget)
                .WrapFileSystemInfo(FileSystem);
        }
#endif

    /// <inheritdoc />
    public override void SetCreationTime(string path, DateTime creationTime)
    {
        path = fileSystem.ValidatePath(path);
        base.SetCreationTime(path, creationTime);
    }

    /// <inheritdoc />
    public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
    {
        path = fileSystem.ValidatePath(path);
        base.SetCreationTimeUtc(path, creationTimeUtc);
    }

    /// <inheritdoc />
    public override void SetCurrentDirectory(string path)
    {
        path = fileSystem.ValidatePath(path);
        base.SetCurrentDirectory(path);
    }

    /// <inheritdoc />
    public override void SetLastAccessTime(string path, DateTime lastAccessTime)
    {
        path = fileSystem.ValidatePath(path);
        base.SetLastAccessTime(path, lastAccessTime);
    }

    /// <inheritdoc />
    public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc)
    {
        path = fileSystem.ValidatePath(path);
        base.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
    }

    /// <inheritdoc />
    public override void SetLastWriteTime(string path, DateTime lastWriteTime)
    {
        path = fileSystem.ValidatePath(path);
        base.SetLastWriteTime(path, lastWriteTime);
    }

    /// <inheritdoc />
    public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
    {
        path = fileSystem.ValidatePath(path);
        base.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateDirectories(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateDirectories(path);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateDirectories(path, searchPattern);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern,
        SearchOption searchOption)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateDirectories(path, searchPattern, searchOption);
    }

#if FEATURE_ENUMERATION_OPTIONS
        /// <inheritdoc />
        public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
			path = fileSystem.ValidatePath(path);
            return base.EnumerateDirectories(path, searchPattern, enumerationOptions);
        }
#endif

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateFiles(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateFiles(path);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateFiles(path, searchPattern);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateFiles(path, searchPattern, searchOption);
    }

#if FEATURE_ENUMERATION_OPTIONS
        /// <inheritdoc />
        public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
			path = fileSystem.ValidatePath(path);
            return base.EnumerateFiles(path, searchPattern, enumerationOptions);
        }
#endif

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateFileSystemEntries(string path)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateFileSystemEntries(path);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateFileSystemEntries(path, searchPattern);
    }

    /// <inheritdoc />
    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern,
        SearchOption searchOption)
    {
        path = fileSystem.ValidatePath(path);
        return base.EnumerateFileSystemEntries(path, searchPattern, searchOption);
    }

#if FEATURE_ENUMERATION_OPTIONS
        /// <inheritdoc />
        public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions)
        {
			path = fileSystem.ValidatePath(path);
            return base.EnumerateFileSystemEntries(path, searchPattern, enumerationOptions);
        }
#endif
}