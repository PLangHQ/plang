using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Attributes
{
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
	public class ExampleAttribute(string plangCode, string mapping) : DescriptionAttribute($"{plangCode} => {mapping}") { }
	
}
