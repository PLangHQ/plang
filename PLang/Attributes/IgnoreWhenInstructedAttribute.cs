﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Attributes
{
	[AttributeUsage(AttributeTargets.Property)]
	public class IgnoreWhenInstructedAttribute : Attribute { }
	
}
