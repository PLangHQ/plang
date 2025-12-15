using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLang.Modules.PythonModule;
using Newtonsoft.Json;
using PLang.Services.OutputStream;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLangTests.Modules.PythonModule
{
    [TestClass]
	public class ProgramTests : BasePLangTest
	{
		PLang.Modules.TerminalModule.Program terminalProgram;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			terminalProgram = new PLang.Modules.TerminalModule.Program(logger, settings, fileSystem, programFactory, engine);
			terminalProgram.Init(container, null, null, null, null);

		}

		[TestMethod]
		public async Task RunPythonScript_InstallRequirements()
		{
			var localPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var pythonRootDir = Path.Join(localPath, "\\Programs\\Python\\");
			fileSystem.AddDirectory(pythonRootDir);
			fileSystem.AddDirectory(Path.Join(pythonRootDir, "Python311"));

			string content = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "main.py"));
			string requirements = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "requirements.txt"));
			fileSystem.AddFile("main.py", new System.IO.Abstractions.TestingHelpers.MockFileData(content));
			fileSystem.AddFile("requirements.txt", new System.IO.Abstractions.TestingHelpers.MockFileData(requirements));
			var outputStream = NSubstitute.Substitute.For<IOutputStreamFactory>();

			
			var p = new Program(fileSystem, logger, settings, signingService, terminalProgram);
			p.Init(container, null, null, null, null);
			string[] vars = new string[] { "result" };
			 await p.RunPythonScript("main.py", variablesToExtractFromPythonScript: vars,
				stdOutVariableName : "stdOut", stdErrorVariableName: "stdError");

			Assert.AreEqual(6.0, memoryStack.Get("result"));

		}

		[TestMethod]
		public async Task RunPythonScript_WithParams()
		{
			var localPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var pythonRootDir = Path.Join(localPath, "\\Programs\\Python\\");
			fileSystem.AddDirectory(pythonRootDir);
			fileSystem.AddDirectory(Path.Join(pythonRootDir, "Python311"));

			string content = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "main_params.py"));
			fileSystem.AddFile("main_params.py", new System.IO.Abstractions.TestingHelpers.MockFileData(content));

			var p = new Program(fileSystem, logger, settings, signingService, terminalProgram);
			p.Init(container, null, null, null, null);

			string[] vars = new string[] { "result" };
			await p.RunPythonScript("main_params.py", variablesToExtractFromPythonScript: vars,
				parameterNames: new string[] { "--num1", "num2" },
				parameterValues: new string[] { "2", "3" },
				stdOutVariableName: "stdOut", stdErrorVariableName: "stdError");

			Assert.AreEqual(5.0, memoryStack.Get("result"));

		}

	}
}
