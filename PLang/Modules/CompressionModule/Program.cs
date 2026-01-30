using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace PLang.Modules.CompressionModule
{
	[Description("compress and decompress(extract) a file or folder. This can be various of file formats, zip, gz, or other custom formats. Example usage: `zip file.txt to file.zip`, or `unzip file.zip to file.txt`")]
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
		public async Task<IError?> CompressFile(string filePath, string saveToPath, int compressionLevel = 0, bool overwrite = false)
		{
			filePath = GetPath(filePath);
			saveToPath = GetPath(saveToPath);
			

			if (!fileSystem.File.Exists(filePath))
			{
				return new ProgramError($"{filePath} does not exist.", Key:"PathNotFound");
			}
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(saveToPath)))
			{
				return new ProgramError($"Directory {Path.GetDirectoryName(saveToPath)} does not exist.", Key: "DirectoryNotFound");
			}

			

			return await archiver.CompressFiles(new string[] { filePath }, saveToPath, compressionLevel, overwrite);
		}


		public async Task<IError?> CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0, bool overwrite = false)
		{
			for (int i=0;i<filePaths.Length;i++) 
			{
				filePaths[i] = GetPath(filePaths[i]);
				if (!fileSystem.File.Exists(filePaths[i]))
				{
					return new ProgramError($"{filePaths[i]} does not exist.", Key: "PathNotFound");
				}
			}
			 
			saveToPath = GetPath(saveToPath);
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(saveToPath)))
			{
				return new ProgramError($"Directory {Path.GetDirectoryName(saveToPath)} does not exist.", Key: "DirectoryNotFound");
			}
			return await archiver.CompressFiles(filePaths, saveToPath, compressionLevel, overwrite);
		}

		public async Task<IError?> DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false)
		{
			sourceArchiveFileName = GetPath(sourceArchiveFileName);
			if (!fileSystem.File.Exists(sourceArchiveFileName))
			{
				return new ProgramError($"{sourceArchiveFileName} does not exist.", Key: "PathNotFound");
			}
			destinationDirectoryName = GetPath(destinationDirectoryName);
			if (!fileSystem.Directory.Exists(destinationDirectoryName))
			{
				fileSystem.Directory.CreateDirectory(destinationDirectoryName);
			}

			await archiver.DecompressFile(sourceArchiveFileName, destinationDirectoryName, overwrite);
			return null;
			
		}

		public async Task<IError?> CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0,
			bool includeBaseDirectory = true, bool createDestinationDirectory = true, bool overwriteDestinationFile = false, string[]? excludePatterns = null
			)
		{
			var absoluteSourceDirectoryName = GetPath(sourceDirectoryName);
			if (!fileSystem.Directory.Exists(absoluteSourceDirectoryName))
			{
				return new ProgramError($"Could not find {sourceDirectoryName}. Looked for it at {absoluteSourceDirectoryName}", Key: "DirectoryNotFound");
			}

			var absoluteDestinationArchiveFileName = GetPath(destinationArchiveFileName);
			if (!fileSystem.Directory.Exists(Path.GetDirectoryName(absoluteDestinationArchiveFileName)))
			{
				if (createDestinationDirectory)
				{
					fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(absoluteDestinationArchiveFileName));
				} else {
					return new ProgramError($"Directory {Path.GetDirectoryName(destinationArchiveFileName)} does not exist.", Key: "DirectoryNotFound");
				}
			}
			if (overwriteDestinationFile && fileSystem.File.Exists(absoluteDestinationArchiveFileName))
			{
				fileSystem.File.Delete(absoluteDestinationArchiveFileName);
			}

			if (excludePatterns != null && excludePatterns.Length > 0)
			{
				List<string> filesToCompress = new();
				var files = fileSystem.Directory.GetFiles(absoluteSourceDirectoryName, "*", SearchOption.AllDirectories);

				List<string> patterns = new();
				foreach (var excludePattern in excludePatterns)
				{
					patterns.Add("^" + Regex.Escape(excludePattern)
									.Replace(@"\*", ".*")    // Convert '*' to '.*'
									.Replace(@"\?", ".")      // Convert '?' to '.'
						  + "$");
				}
				


				foreach (var file in files)
				{
					if (patterns.Any(pattern => Regex.IsMatch(file, pattern)))
					{
						continue;
					}
					filesToCompress.Add(file);
				}
				return await CompressFiles(filesToCompress.ToArray(), absoluteDestinationArchiveFileName, compressionLevel, overwriteDestinationFile);
			}
			else
			{
				return await archiver.CompressDirectory(absoluteSourceDirectoryName, absoluteDestinationArchiveFileName, compressionLevel, includeBaseDirectory);
			}
		}
	}
}
