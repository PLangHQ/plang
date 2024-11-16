using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;

namespace PLangTests.Mocks;

public class PLangMockFileSystem : MockFileSystem, IPLangFileSystem
{
    public string RootDirectory => Environment.CurrentDirectory;

    public bool IsRootApp => true;

    public string RelativeAppPath => Path.DirectorySeparatorChar.ToString();

    public string SharedPath => Path.DirectorySeparatorChar.ToString();
    public string GoalsPath => RootDirectory;
    public string BuildPath => Path.Join(RootDirectory, ".build");
    public string DbPath => Path.Join(RootDirectory, ".db");

    public string? ValidatePath(string? path)
    {
        return path;
    }

    public void SetFileAccess(List<FileAccessControl> fileAccesses)
    {
    }

    public void AddStep(GoalStep step)
    {
        AddFile(step.AbsolutePrFilePath, JsonConvert.SerializeObject(step));
    }

    public void AddInstruction(string path, Instruction instruction)
    {
        AddFile(path, JsonConvert.SerializeObject(instruction));
    }

    public void Init(ISettings settings, ILogger logger, ILlmService llmService)
    {
    }
}