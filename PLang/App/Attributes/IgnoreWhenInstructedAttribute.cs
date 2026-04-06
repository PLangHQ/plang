using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Attributes
{
	[AttributeUsage(AttributeTargets.Property)]
	public class IgnoreWhenInstructedAttribute : Attribute { }
	
}
