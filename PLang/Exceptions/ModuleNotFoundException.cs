using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	internal class ModuleNotFoundException : Exception
	{
		public ModuleNotFoundException(string message) : base(message) { }
	}
}
