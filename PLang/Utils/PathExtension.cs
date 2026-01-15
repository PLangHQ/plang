using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public static class PathExtension
	{
		public static string AdjustPathToOs(this string path)
		{
			if (string.IsNullOrEmpty(path)) return path;
			
			if (Path.DirectorySeparatorChar == '\\')
			{
				path = path.Replace('/', Path.DirectorySeparatorChar);
				if (path.StartsWith("\\\\"))
				{
					path = "\\\\" + path.Substring(2).Replace("\\\\", "\\");
				}
				else
				{
					path = path.Replace("\\\\", "\\");
				}
				return path;
			}
			else
			{
				path = path.Replace('\\', Path.DirectorySeparatorChar);
				if (path.StartsWith("//"))
				{
					path = "//" + path.Substring(2).Replace("//", "/");
				}
				else
				{
					path = path.Replace("//", "/");
				}
				return path;
			}

		}

		public static string RemoveExtension(this string path)
		{
			if (!path.Contains(".")) return path;
			return path.Substring(0, path.LastIndexOf("."));
		}
	}
}
