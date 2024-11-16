using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.TerminalModule;
using PLang.Services.OutputStream;

namespace PLangTests.Modules.PythonModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    private Program terminalProgram;

    [TestInitialize]
    public void Init()
    {
        Initialize();
        terminalProgram = new Program(logger, settings, outputStreamFactory, fileSystem);
        terminalProgram.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory,
            settings, appCache, null);
    }

    [TestMethod]
    public async Task RunPythonScript_InstallRequirements()
    {
        var localPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pythonRootDir = Path.Join(localPath, "\\Programs\\Python\\");
        fileSystem.AddDirectory(pythonRootDir);
        fileSystem.AddDirectory(Path.Join(pythonRootDir, "Python311"));

        var content = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "main.py"));
        var requirements = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "requirements.txt"));
        fileSystem.AddFile("main.py", new MockFileData(content));
        fileSystem.AddFile("requirements.txt", new MockFileData(requirements));
        var outputStream = Substitute.For<IOutputStreamFactory>();


        var p = new PLang.Modules.PythonModule.Program(fileSystem, logger, settings, outputStream, signingService,
            terminalProgram);
        p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings,
            appCache, null);
        string[] vars = new[] { "result" };
        await p.RunPythonScript(variablesToExtractFromPythonScript: vars,
            stdOutVariableName: "stdOut", stdErrorVariableName: "stdError");

        Assert.AreEqual(6.0, memoryStack.Get("result"));
    }

    [TestMethod]
    public async Task RunPythonScript_WithParams()
    {
        var localPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pythonRootDir = Path.Join(localPath, "\\Programs\\Python\\");
        fileSystem.AddDirectory(pythonRootDir);
        fileSystem.AddDirectory(Path.Join(pythonRootDir, "Python311"));

        var content = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "main_params.py"));
        fileSystem.AddFile("main_params.py", new MockFileData(content));

        var outputStream = Substitute.For<IOutputStreamFactory>();
        var p = new PLang.Modules.PythonModule.Program(fileSystem, logger, settings, outputStream, signingService,
            terminalProgram);
        p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings,
            appCache, null);

        string[] vars = new[] { "result" };
        await p.RunPythonScript("main_params.py", variablesToExtractFromPythonScript: vars,
            parameterNames: new[] { "--num1", "num2" },
            parameterValues: new[] { "2", "3" },
            stdOutVariableName: "stdOut", stdErrorVariableName: "stdError");

        Assert.AreEqual(5.0, memoryStack.Get("result"));
    }
}