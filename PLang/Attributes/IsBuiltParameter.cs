using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Attributes
{
	[AttributeUsage(AttributeTargets.All)]
	public class IsBuiltParameter(string Type) : Attribute
	{
		public string Type { get; } = Type;
	}

}
