using PLang.Interfaces;

namespace PLang.Modules.CompressionModule
{
	public class Program : BaseProgram
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IArchiver archiver;

		public Program(IPLangFileSystem fileSystem, IArchiver archiver)
		{
			this.fileSystem = fileSystem;
			this.archiver = archiver;
		}

		public async Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
		{
			filePath = GetPath(filePath);
			saveToPath = GetPath(saveToPath);
			

			if (!fileSystem.File.Exists(filePath))
			{
				throw new FileNotFoundException($"{filePath} does not exist.");
			}
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(saveToPath)))
			{
				throw new DirectoryNotFoundException($"Directory {Path.GetDirectoryName(saveToPath)} does not exist.");
			}

			

			await archiver.CompressFiles(new string[] { filePath }, saveToPath, compressionLevel);
		}

		private string GetPath(string path)
		{
			if (!Path.IsPathRooted(path))
			{
				path = Path.Join(fileSystem.RootDirectory, path);
			}
			return path;
		}

		public async Task CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0)
		{
			for (int i=0;i<filePaths.Length;i++) 
			{
				filePaths[i] = GetPath(filePaths[i]);
				if (!fileSystem.File.Exists(filePaths[i]))
				{
					throw new FileNotFoundException($"{filePaths[i]} does not exist.");
				}
			}

			saveToPath = GetPath(saveToPath);
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(saveToPath)))
			{
				throw new DirectoryNotFoundException($"Directory {Path.GetDirectoryName(saveToPath)} does not exist.");
			}
			await archiver.CompressFiles(filePaths, saveToPath, compressionLevel);
		}

		public async Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false)
		{
			sourceArchiveFileName = GetPath(sourceArchiveFileName);
			if (!fileSystem.File.Exists(sourceArchiveFileName))
			{
				throw new FileNotFoundException($"{sourceArchiveFileName} does not exist.");
			}
			destinationDirectoryName = GetPath(destinationDirectoryName);
			if (!fileSystem.Directory.Exists(destinationDirectoryName))
			{
				fileSystem.Directory.CreateDirectory(destinationDirectoryName);
			}

			await archiver.DecompressFile(sourceArchiveFileName, destinationDirectoryName, overwrite);
			
		}

		public async Task CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0, bool includeBaseDirectory = true)
		{
			sourceDirectoryName = GetPath(sourceDirectoryName);
			if (!fileSystem.Directory.Exists(sourceDirectoryName))
			{
				fileSystem.Directory.CreateDirectory(sourceDirectoryName);
			}

			destinationArchiveFileName = GetPath(destinationArchiveFileName);
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(destinationArchiveFileName)))
			{
				throw new DirectoryNotFoundException($"Directory {Path.GetDirectoryName(destinationArchiveFileName)} does not exist.");
			}

			await archiver.CompressDirectory(sourceDirectoryName, destinationArchiveFileName, compressionLevel, includeBaseDirectory);
		}
	}
}
