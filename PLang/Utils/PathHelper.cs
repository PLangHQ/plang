using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using System.Text;
using System.Xml.Linq;

namespace PLang.Utils
{
	public class PathHelper
	{

		public static bool IsTemplateFile(string path)
		{
			if (path.Contains("\n") || path.Contains("\r") || path.Contains("\r")) return false;
			if (Encoding.UTF8.GetByteCount(path) > 255) return false;
			string ext = Path.GetExtension(path);
			return (!string.IsNullOrEmpty(ext) && ext.Length < 10);
		}
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
