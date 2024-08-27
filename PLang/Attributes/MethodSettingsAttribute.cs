using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Attributes
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class MethodSettingsAttribute : Attribute
	{
		public bool CanBeCached { get; set; }
		public bool CanHaveErrorHandling { get; set; }
		public bool CanBeAsync { get; set; }

		public MethodSettingsAttribute(bool canBeCached = true, bool canHaveErrorHandling = true, bool canBeAsync = true)
		{
			CanBeCached = canBeCached;
			CanHaveErrorHandling = canHaveErrorHandling;
			CanBeAsync = canBeAsync;
		}
	}
}
