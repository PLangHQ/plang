using System.IO.Compression;

namespace PLang.Interfaces
{
	public interface IArchiver
	{
		Task CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0);
		Task CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName, int compressionLevel = 0, bool includeBaseDirectory = true);
		Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false);
	}
}
