using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using System.IO.Compression;

namespace PLang.Services.ArchiveService
{
	public class Zip : IArchiver
	{
		private readonly IPLangFileSystem fileSystem;

		public Zip(IPLangFileSystem fileSystem)
		{
			this.fileSystem = fileSystem;
		}

		public async Task<IError?> CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0, bool includeBaseDirectory = true, bool overwrite = false)
		{
			var error = OverwriteCheck(destinationArchiveFileName, overwrite);
			if (error != null) return error;

			ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, (CompressionLevel)compressionLevel, includeBaseDirectory);
			return null;
		}

		public async Task<IError?> CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
		{
			return await CompressFiles(new string[] { filePath }, saveToPath, compressionLevel);
		}

		public async Task<IError?> CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0, bool overwrite = false)
		{
			if (filePaths.Length == 0) return new ProgramError("No files to complress");

			var error = OverwriteCheck(saveToPath, overwrite);
			if (error != null) return error;

			var commonPath = FindCommonBaseDirectory(filePaths.ToList());
			using (var zipFileStream = new FileStream(saveToPath, FileMode.Create))
			using (ZipArchive archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
			{
				foreach (var filePath in filePaths)
				{
					if (saveToPath == filePath) continue;

					string fileName = filePath.Replace(commonPath, "").TrimStart(Path.DirectorySeparatorChar);					
					 archive.CreateEntryFromFile(filePath, fileName, (CompressionLevel) compressionLevel);
				
				}

			}
			return null;
		}

		private IError? OverwriteCheck(string saveToPath, bool overwrite)
		{
			if (fileSystem.File.Exists(saveToPath))
			{
				if (overwrite)
				{
					fileSystem.File.Delete(saveToPath);
				}
				else
				{
					return new ServiceError($"Destination file already exists", GetType());
				}
			}
			return null;
		}

		private string FindCommonBaseDirectory(List<string> filePaths)
		{
			if (filePaths == null || filePaths.Count == 0)
				return string.Empty;

			string GetDirectory(string path) => Path.GetDirectoryName(path) ?? string.Empty;

			string commonPath = GetDirectory(filePaths[0]);

			foreach (var path in filePaths)
			{
				string currentPath = GetDirectory(path);
				while (!currentPath.StartsWith(commonPath, StringComparison.OrdinalIgnoreCase))
				{
					if (commonPath.Length == 0)
						return string.Empty; 

					commonPath = GetDirectory(commonPath.Substring(0, commonPath.Length - 1));
				}
			}

			return commonPath;
		}

		public async Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false)
		{
			ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwrite);
		}
	}
}
