using PLang.Interfaces;
using System.ComponentModel;
using System.Text.RegularExpressions;

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

		[Description("compressionLevel: 0=Optimal, 1=Fastest, 2=No compression, 3=Smallest size(highest compression)")]
		public async Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0, bool overwrite = false)
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

			

			await archiver.CompressFiles(new string[] { filePath }, saveToPath, compressionLevel, overwrite);
		}


		public async Task CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0, bool overwrite = false)
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
			await archiver.CompressFiles(filePaths, saveToPath, compressionLevel, overwrite);
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

		public async Task CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0,
			bool includeBaseDirectory = true, bool createDestinationDirectory = true, bool overwriteDestinationFile = false, string[]? excludePatterns = null
			)
		{
			sourceDirectoryName = GetPath(sourceDirectoryName);
			if (!fileSystem.Directory.Exists(sourceDirectoryName))
			{
				fileSystem.Directory.CreateDirectory(sourceDirectoryName);
			}

			destinationArchiveFileName = GetPath(destinationArchiveFileName);
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(destinationArchiveFileName)))
			{
				if (createDestinationDirectory)
				{
					fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(destinationArchiveFileName));
				} else {
					throw new DirectoryNotFoundException($"Directory {Path.GetDirectoryName(destinationArchiveFileName)} does not exist.");
				}
			}
			if (overwriteDestinationFile && fileSystem.File.Exists(destinationArchiveFileName))
			{
				fileSystem.File.Delete(destinationArchiveFileName);
			}

			if (excludePatterns != null)
			{
				List<string> filesToCompress = new();
				var files = fileSystem.Directory.GetFiles(sourceDirectoryName, "*", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					if (excludePatterns != null && excludePatterns.Any(pattern => Regex.IsMatch(file, pattern)))
					{
						continue;
					}
					filesToCompress.Add(file);
				}
				await CompressFiles(filesToCompress.ToArray(), destinationArchiveFileName, compressionLevel, overwriteDestinationFile);
			}
			else
			{
				await archiver.CompressDirectory(sourceDirectoryName, destinationArchiveFileName, compressionLevel, includeBaseDirectory);
			}
		}
	}
}
