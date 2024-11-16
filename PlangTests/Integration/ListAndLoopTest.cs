using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using PLang.Modules.FileModule;

namespace PLangTests.Integration;

[TestClass]
public class ListAndLoopTest : BasePLangTest
{
    private Program fileProgram;
    private PLang.Modules.LoopModule.Program loopProgram;

    [TestInitialize]
    public void Init()
    {
        Initialize();

        fileProgram = new Program(fileSystem, settings, logger, pseudoRuntime, engine);
        fileProgram.Init(container, null, null, null, memoryStack, null, null, null, null, null, null, null);
        var goal = new Goal();
        goal.RelativeAppStartupFolderPath = "/";

        loopProgram = new PLang.Modules.LoopModule.Program(logger, pseudoRuntime, engine);
        loopProgram.Init(container, goal, null, null, memoryStack, null, null, null, null, null, null, null);
    }

    [TestMethod]
    public async Task TestListAndLoop()
    {
        var path = "Test100x10.xlsx";
        var fullPath = Path.Combine(fileSystem.RootDirectory, path);
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] fileBytes;
        using (var reader = new BinaryReader(stream))
        {
            fileBytes = reader.ReadBytes((int)stream.Length);
        }

        fileSystem.AddFile(fullPath, new MockFileData(fileBytes));
        await fileProgram.ReadExcelFile(path, false);


        await loopProgram.RunLoop("Sheet1", "Test", new Dictionary<string, object?>());
    }
}