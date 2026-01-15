using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.OutputStream.Messages;
using Sprache;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PLang.Utils
{
	public class PathHelper
	{
		static readonly char[] InvalidNameChars =
	(System.IO.Path.GetInvalidFileNameChars().Concat(new[] { ':', '*', '?', '"', '<', '>', '|' })).Distinct().ToArray();

		public static bool IsTemplateFile(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return false;
			if (input.IndexOfAny(new[] { '\n', '\r' }) >= 0) return false;
			input = input.Trim();
			if (Encoding.UTF8.GetByteCount(input) > 255) return false;

			var parts = input.Split(new[] { '/', '\\' }, StringSplitOptions.None);
			if (parts.Length == 0) return false;

			// filename (last segment) must exist and have a short extension
			var file = parts[^1];
			if (string.IsNullOrWhiteSpace(file)) return false;
			if (file.IndexOfAny(InvalidNameChars) >= 0) return false;

			var ext = System.IO.Path.GetExtension(file);
			if (string.IsNullOrEmpty(ext) || ext.Length < 2 || ext.Length > 10) return false; // includes the dot
			var nameNoExt = System.IO.Path.GetFileNameWithoutExtension(file);
			if (string.IsNullOrWhiteSpace(nameNoExt)) return false;
			if (file.EndsWith(" ") || file.EndsWith(".")) return false; // Win-safe

			// directories: valid names, and (by design) no whitespace to avoid matching sentences
			for (int i = 0; i < parts.Length - 1; i++)
			{
				var seg = parts[i];
				if (string.IsNullOrEmpty(seg)) continue; // allow leading "/" or "C:\"
				if (seg.IndexOfAny(InvalidNameChars) >= 0) return false;
				if (seg.Any(char.IsWhiteSpace)) return false; // tighten to avoid natural-language text
				if (seg.EndsWith(" ") || seg.EndsWith(".")) return false;
			}

			// reject if input contains obvious sentence punctuation outside path charset
			if (!Regex.IsMatch(input, @"^[\p{L}\p{N}\s._\-\\/]+$")) return false;

			return true;
		}
		public static string GetSystemPath(string? path, IPLangFileSystem fileSystem, Goal goal)
		{
			return fileSystem.Path.Join(fileSystem.SystemDirectory, goal.RelativeGoalFolderPath, path);
		}

		public static string GetPath(string? path, IPLangFileSystem fileSystem, Goal? goal)
		{
			if (string.IsNullOrEmpty(path))
			{
				path = fileSystem.GoalsPath;
			}
			var pathWithDirSep = path.AdjustPathToOs();

			string startOfPath = (pathWithDirSep.Length > 3) ? pathWithDirSep.Substring(0, 3) : pathWithDirSep;
			if (startOfPath == (fileSystem.Path.DirectorySeparatorChar.ToString() + fileSystem.Path.DirectorySeparatorChar.ToString() + fileSystem.Path.DirectorySeparatorChar.ToString()))
			{
				var absolutePath = pathWithDirSep.Substring(1);
				return fileSystem.Path.GetFullPath(absolutePath);
			}


			startOfPath = (pathWithDirSep.Length > 2) ? pathWithDirSep.Substring(0, 2) : pathWithDirSep;
			if (startOfPath == (fileSystem.Path.DirectorySeparatorChar.ToString() + fileSystem.Path.DirectorySeparatorChar.ToString()))
			{
				var absolutePath = JoinRootWithPath(fileSystem, fileSystem.Path.GetPathRoot(fileSystem.RootDirectory), pathWithDirSep);
				return fileSystem.Path.GetFullPath(absolutePath);
			}

			//allow c:\file.txt path
			if (Environment.OSVersion.Platform == PlatformID.Win32NT && fileSystem.Path.IsPathRooted(pathWithDirSep) && !pathWithDirSep.StartsWith(fileSystem.Path.DirectorySeparatorChar.ToString()))
			{
				return pathWithDirSep;
			}
			

			if (pathWithDirSep.StartsWith(fileSystem.Path.DirectorySeparatorChar.ToString()))
			{
				pathWithDirSep = JoinRootWithPath(fileSystem, fileSystem.RootDirectory, pathWithDirSep);
				return fileSystem.Path.GetFullPath(pathWithDirSep);
			}
			else
			{
				if (goal != null && goal.AbsoluteGoalFolderPath.StartsWith(fileSystem.RootDirectory))
				{
					pathWithDirSep = JoinRootWithPath(fileSystem, goal.AbsoluteGoalFolderPath, pathWithDirSep);
				}
				else
				{
					pathWithDirSep = JoinRootWithPath(fileSystem, fileSystem.RootDirectory, pathWithDirSep);
				}
				pathWithDirSep = fileSystem.Path.GetFullPath(pathWithDirSep);
			}
			return pathWithDirSep;
		}

		public static string JoinRootWithPath(IPLangFileSystem fileSystem, string rootPath, string path2)
		{
			if (path2.StartsWith(rootPath)) return path2;
			return fileSystem.Path.Join(rootPath, path2);
		}
	}

}
