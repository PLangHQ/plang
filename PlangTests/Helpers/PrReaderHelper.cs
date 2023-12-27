using PLang.Building.Model;
using PLang.Building.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Helpers
{
	public class PrReaderHelper
	{

		public static string GetPrFileRaw(string fileName)
		{
			// Get the current assembly's directory
			var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			// Combine with the relative path to the examples folder
			var filePath = Path.Combine(assemblyDirectory, "PrFiles", fileName);

			return File.ReadAllText(filePath);
		}
	}
}
