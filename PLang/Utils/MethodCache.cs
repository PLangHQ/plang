using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	internal static class MethodCache
	{

		public static Dictionary<string, MethodInfo> Cache = new Dictionary<string, MethodInfo>();
	}
}
