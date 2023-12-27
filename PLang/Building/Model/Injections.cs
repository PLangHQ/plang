using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public class Injections
	{
		public Injections(string type, string path, bool isGlobal)
		{
			Type = type;
			Path = path;
			IsGlobal = isGlobal;
		}
	

		public string Type { get; set; }
		public string Path { get; set; }
		public bool IsGlobal { get; set; }
	}
}
