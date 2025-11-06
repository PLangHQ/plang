using System.Reflection;

namespace PLang.Utils
{
	public class PlangHelper
	{

		public static string GetVersion()
		{
			var assembly = Assembly.GetAssembly(typeof(PlangHelper));
			return assembly.GetName().Version.ToString();
		}
	}
}
