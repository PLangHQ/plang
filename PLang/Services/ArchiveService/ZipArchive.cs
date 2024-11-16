using System.IO.Compression;
using PLang.Exceptions;
using PLang.Interfaces;

namespace PLang.Services.ArchiveService;

public class Zip : IArchiver
{
    private readonly IPLangFileSystem fileSystem;

    public Zip(IPLangFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public async Task CompressDirectory(string sourceDirectoryName, string destinationArchiveFileName,
        int compressionLevel = 0, bool includeBaseDirectory = true, bool overwrite = false)
    {
        OverwriteCheck(destinationArchiveFileName, overwrite);

        ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, (CompressionLevel)compressionLevel,
            includeBaseDirectory);
    }

    public async Task CompressFiles(string[] filePaths, string saveToPath, int compressionLevel = 0,
        bool overwrite = false)
    {
        OverwriteCheck(saveToPath, overwrite);

        var commonPath = FindCommonBaseDirectory(filePaths.ToList());
        using (var zipFileStream = new FileStream(saveToPath, FileMode.Create))
        using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
        {
            foreach (var filePath in filePaths)
            {
                if (saveToPath == filePath) continue;

                var fileName = filePath.Replace(commonPath, "").TrimStart(Path.DirectorySeparatorChar);
                archive.CreateEntryFromFile(filePath, fileName, (CompressionLevel)compressionLevel);
            }
        }
    }

    public async Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName,
        bool overwrite = false)
    {
        ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwrite);
    }

    public async Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
    {
        await CompressFiles(new[] { filePath }, saveToPath, compressionLevel);
    }

    private void OverwriteCheck(string saveToPath, bool overwrite)
    {
        if (fileSystem.File.Exists(saveToPath))
        {
            if (overwrite)
                fileSystem.File.Delete(saveToPath);
            else
                throw new RuntimeException("Destination file already exists");
        }
    }

    private string FindCommonBaseDirectory(List<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0)
            return string.Empty;

        string GetDirectory(string path)
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }

        var commonPath = GetDirectory(filePaths[0]);

        foreach (var path in filePaths)
        {
            var currentPath = GetDirectory(path);
            while (!currentPath.StartsWith(commonPath, StringComparison.OrdinalIgnoreCase))
            {
                if (commonPath.Length == 0)
                    return string.Empty;

                commonPath = GetDirectory(commonPath.Substring(0, commonPath.Length - 1));
            }
        }

        return commonPath;
    }
}