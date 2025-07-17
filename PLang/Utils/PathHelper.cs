using PLang.Building.Model;
using PLang.Interfaces;

namespace PLang.Utils
{
	public class PathHelper
	{


		public static string GetSystemPath(string? path, IPLangFileSystem fileSystem, Goal goal)
		{
			return string.Join(fileSystem.SystemDirectory, path);
		}

		public static string GetPath(string? path, IPLangFileSystem fileSystem, Goal? goal)
		{
			if (string.IsNullOrEmpty(path))
			{
				path = fileSystem.GoalsPath;
			}
			var pathWithDirSep = path.AdjustPathToOs();

			string startOfPath = (pathWithDirSep.Length > 3) ? pathWithDirSep.Substring(0, 3) : pathWithDirSep;
			if (startOfPath == (Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString()))
			{
				var absolutePath = pathWithDirSep.Substring(1);
				return Path.GetFullPath(absolutePath);
			}
			
			startOfPath = (pathWithDirSep.Length > 2) ? pathWithDirSep.Substring(0, 2) : pathWithDirSep;
			if (startOfPath == (Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString()))
			{
				var absolutePath = Path.Join(Path.GetPathRoot(fileSystem.RootDirectory), pathWithDirSep);
				return Path.GetFullPath(absolutePath);
			}

			//allow c:\file.txt path
			if (Environment.OSVersion.Platform == PlatformID.Win32NT && Path.IsPathRooted(pathWithDirSep) && !pathWithDirSep.StartsWith(Path.DirectorySeparatorChar.ToString()))
			{
				return pathWithDirSep;
			}
			

			if (pathWithDirSep.StartsWith(Path.DirectorySeparatorChar.ToString()))
			{
				pathWithDirSep = Path.Join(fileSystem.RootDirectory, pathWithDirSep);
				return Path.GetFullPath(pathWithDirSep);
			}
			else
			{
				if (goal != null && goal.AbsoluteGoalFolderPath.StartsWith(fileSystem.RootDirectory))
				{
					pathWithDirSep = Path.Join(goal.AbsoluteGoalFolderPath, pathWithDirSep);
				}
				else
				{
					pathWithDirSep = Path.Join(fileSystem.RootDirectory, pathWithDirSep);
				}
				pathWithDirSep = Path.GetFullPath(pathWithDirSep);
			}
			return pathWithDirSep;
		}

	}
}
