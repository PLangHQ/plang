using System.IO.Abstractions;
using PLang.SafeFileSystem;

namespace PLang.Interfaces;

public interface IPLangFileSystem : IFileSystem
{
    public string RootDirectory { get; }
    public bool IsRootApp { get; }
    public string RelativeAppPath { get; }
    public string SharedPath { get; }
    public string GoalsPath { get; }
    public string BuildPath { get; }
    public string DbPath { get; }

    void SetFileAccess(List<FileAccessControl> fileAccesses);
    public string ValidatePath(string? path);
}