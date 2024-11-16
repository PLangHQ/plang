using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.CompressionModule;

namespace PLangTests.Modules.CompressionModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }

    [TestMethod]
    public async Task CompressFile_Test()
    {
        var filePath = Path.Join(fileSystem.RootDirectory, "file.txt");
        var saveToPath = "c:\\file.zip";

        fileSystem.AddFile(filePath, new MockFileData(""));

        var p = new Program(fileSystem, archiver);
        await p.CompressFile(filePath, saveToPath);

        await archiver.Received(1).CompressFiles(Arg.Is<string[]>(p => p[0] == filePath), saveToPath);
    }


    [TestMethod]
    public async Task CompressFiles_Test()
    {
        string[] filePaths =
            { Path.Join(fileSystem.RootDirectory, "file.txt"), Path.Join(fileSystem.RootDirectory, "file2.txt") };
        var saveToPath = "c:\\file.zip";

        fileSystem.AddFile(filePaths[0], new MockFileData(""));
        fileSystem.AddFile(filePaths[1], new MockFileData(""));

        var p = new Program(fileSystem, archiver);
        await p.CompressFiles(filePaths, saveToPath);

        await archiver.Received(1)
            .CompressFiles(Arg.Is<string[]>(p => p[0] == filePaths[0] && p[1] == filePaths[1]), saveToPath);
    }


    [TestMethod]
    public async Task CompressDirectory_Test()
    {
        var dirPath = "c:\\temp\\";
        var saveToPath = "c:\\file.zip";

        fileSystem.AddDirectory(dirPath);

        var p = new Program(fileSystem, archiver);
        await p.CompressDirectory(dirPath, saveToPath);

        await archiver.Received(1).CompressDirectory(dirPath, saveToPath);
    }


    [TestMethod]
    public async Task Decompress_Test()
    {
        var zipFile = "c:\\temp\\file.zip";
        var saveToPath = "c:\\file\\";

        fileSystem.AddFile(zipFile, new MockFileData(""));

        var p = new Program(fileSystem, archiver);
        await p.DecompressFile(zipFile, saveToPath);

        await archiver.Received(1).DecompressFile(zipFile, saveToPath);
    }
}