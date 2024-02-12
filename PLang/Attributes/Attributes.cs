using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Attributes
{
	[AttributeUsage(AttributeTargets.Parameter)]
	public class HandlesVariableAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class BuilderHelperAttribute : Attribute
	{
		public string MethodName { get; }
		public string? Explaination { get; }

		public BuilderHelperAttribute(string methodName, string? explaination = null)
		{
			MethodName = methodName;
			Explaination = explaination;
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class VisibleInheritationAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Method)]
	public class RunOnBuildAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
	public class DefaultValueAttribute : Attribute
	{
		public object Value { get; set; }
		public DefaultValueAttribute(object value)
		{
			Value = value;
		}
	}

}
