using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public class Injections
	{
		public Injections(string type, string path, bool isGlobal, string? environmentVariable = "PLANG_ENV", string? environmentVariableValue = null)
		{
			Type = type;
			Path = path;
			IsGlobal = isGlobal;
			EnvironmentVariable = environmentVariable;
			EnvironmentVariableValue = environmentVariableValue;
		}
		public bool AtSignInjection = false;

		public string Type { get; set; }
		public string Path { get; set; }
		public bool IsGlobal { get; set; }
		public string? EnvironmentVariable { get; set; }
		public string? EnvironmentVariableValue { get; set; }
	}
}
