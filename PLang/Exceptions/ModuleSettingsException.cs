using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class ModuleSettingsException : Exception
	{
		public ModuleSettingsException(string message) : base(message) { }
	}
}
