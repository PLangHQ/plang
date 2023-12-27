using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Events
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
	public class DefaultValueAttribute : Attribute
	{
		public string Value { get; set; }	
		public DefaultValueAttribute(string value) {
			Value = value;
		}
	}
	
}
