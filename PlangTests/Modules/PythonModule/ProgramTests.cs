using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLang.Modules.PythonModule;
using Newtonsoft.Json;
using PLang.Services.OutputStream;

namespace PLangTests.Modules.PythonModule
{
    [TestClass]
	public class ProgramTests : BasePLangTest
	{
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
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
			var outputStream = NSubstitute.Substitute.For<IOutputStream>();
			var p = new Program(fileSystem, logger, settings, outputStream, signingService);

			string[] vars = new string[] { "result" };
			var result = await p.RunPythonScript("main.py", variablesToExtractFromPythonScript: vars,
				stdOutVariableName : "stdOut", stdErrorVariableName: "stdError");

			Assert.AreEqual(6.0, result["result"]);

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

			var outputStream = NSubstitute.Substitute.For<IOutputStream>();
			var p = new Program(fileSystem, logger, settings, outputStream, signingService);

			string[] vars = new string[] { "result" };
			var result = await p.RunPythonScript("main_params.py", variablesToExtractFromPythonScript: vars,
				parameterNames: new string[] { "--num1", "num2" },
				parameterValues: new string[] { "2", "3" },
				stdOutVariableName: "stdOut", stdErrorVariableName: "stdError");

			Assert.AreEqual(5.0, result["result"]);

		}

	}
}
