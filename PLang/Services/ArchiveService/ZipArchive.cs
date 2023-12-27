using PLang.Interfaces;
using System.IO.Compression;

namespace PLang.Services.ArchiveService
{
	public class Zip : IArchiver
	{
		public Zip()
		{
		}

		public async Task CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0, bool includeBaseDirectory = true)
		{
			ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, (CompressionLevel)compressionLevel, includeBaseDirectory);
		}

		public async Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
		{
			await CompressFiles(new string[] { filePath }, saveToPath, compressionLevel);
		}

		public async Task CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0)
		{
			using (var zipFileStream = new FileStream(saveToPath, FileMode.Create))
			using (ZipArchive archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
			{
				foreach (var filePath in filePaths)
				{
					string fileName = Path.GetFileName(filePath);
					archive.CreateEntryFromFile(filePath, fileName, (CompressionLevel) compressionLevel);
				}
			}
		}

		public async Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false)
		{
			ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwrite);
		}
	}
}
