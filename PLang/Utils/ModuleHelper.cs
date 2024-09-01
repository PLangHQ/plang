using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	internal class ModuleHelper
	{
		private readonly TypeHelper typeHelper;

		public ModuleHelper(TypeHelper typeHelper)
		{
			this.typeHelper = typeHelper;
		}
		public object GetModule(string name)
		{
			var typeObj = typeHelper.GetRuntimeType(name);
			return typeHelper.GetProgramInstance(typeObj.FullName);
		}
	}
}
