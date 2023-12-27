using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.CompressionModule;
using System.IO.Abstractions.TestingHelpers;

namespace PLangTests.Modules.CompressionModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		[TestInitialize] 
		public void Init() {
			base.Initialize();
		}	

		[TestMethod]
		public async Task CompressFile_Test()
		{

			string filePath = Path.Join(fileSystem.RootDirectory, "file.txt");
			string saveToPath = "c:\\file.zip";

			fileSystem.AddFile(filePath, new MockFileData(""));

			var p = new Program(fileSystem, archiver);
			await p.CompressFile(filePath, saveToPath, 0);

			await archiver.Received(1).CompressFiles(Arg.Is<string[]>(p => p[0] == filePath), saveToPath, 0);

		}


		[TestMethod]
		public async Task CompressFiles_Test()
		{

			string[] filePaths = { Path.Join(fileSystem.RootDirectory, "file.txt"), Path.Join(fileSystem.RootDirectory, "file2.txt") };
			string saveToPath = "c:\\file.zip";

			fileSystem.AddFile(filePaths[0], new MockFileData(""));
			fileSystem.AddFile(filePaths[1], new MockFileData(""));

			var p = new Program(fileSystem, archiver);
			await p.CompressFiles(filePaths, saveToPath, 0);

			await archiver.Received(1).CompressFiles(Arg.Is<string[]>(p => p[0] == filePaths[0] && p[1] == filePaths[1]), saveToPath, 0);

		}


		[TestMethod]
		public async Task CompressDirectory_Test()
		{

			string dirPath = "c:\\temp\\";
			string saveToPath = "c:\\file.zip";

			fileSystem.AddDirectory(dirPath);

			var p = new Program(fileSystem, archiver);
			await p.CompressDirectory(dirPath, saveToPath, 0);

			await archiver.Received(1).CompressDirectory(dirPath, saveToPath, 0);

		}


		[TestMethod]
		public async Task Decompress_Test()
		{

			string zipFile = "c:\\temp\\file.zip";
			string saveToPath = "c:\\file\\";

			fileSystem.AddFile(zipFile, new MockFileData(""));

			var p = new Program(fileSystem, archiver);
			await p.DecompressFile(zipFile, saveToPath);

			await archiver.Received(1).DecompressFile(zipFile, saveToPath);

		}

	}
}
