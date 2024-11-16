namespace PLang.Utils;

public class DirectoryHelper
{
    public static void Copy(string sourceDir, string destinationDir, bool copySubDirs = true)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");

        var dir = new DirectoryInfo(sourceDir);
        DirectoryInfo[] dirs = dir.GetDirectories();

        if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        if (copySubDirs)
            foreach (var subdir in dirs)
            {
                var targetSubDirPath = Path.Combine(destinationDir, subdir.Name);
                Copy(subdir.FullName, targetSubDirPath, copySubDirs);
            }
    }
}