using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	public class ErrorHandler
	{
		public bool IgnoreErrors { get; set; } = false;
		public Dictionary<string, string>? OnExceptionContainingTextCallGoal { get; set; } = null;
	}
}
