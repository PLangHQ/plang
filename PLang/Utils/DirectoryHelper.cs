namespace PLang.Utils
{
	public class DirectoryHelper
	{
		public static void Copy(string sourceDir, string destinationDir, bool copySubDirs = true)
		{
			if (!Directory.Exists(sourceDir))
			{
				throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");
			}
			 
			DirectoryInfo dir = new DirectoryInfo(sourceDir);
			DirectoryInfo[] dirs = dir.GetDirectories();

			if (!Directory.Exists(destinationDir))
			{
				Directory.CreateDirectory(destinationDir);
			}

			foreach (FileInfo file in dir.GetFiles())
			{
				string targetFilePath = System.IO.Path.Join(destinationDir, file.Name);
				file.CopyTo(targetFilePath, true);
			}

			if (copySubDirs)
			{
				foreach (DirectoryInfo subdir in dirs)
				{
					string targetSubDirPath = System.IO.Path.Join(destinationDir, subdir.Name);
					Copy(subdir.FullName, targetSubDirPath, copySubDirs);
				}
			}

		}
	}
}
