using PLang.Building.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class RuntimeException : Exception
	{
		Goal? goal;
		public RuntimeException(string message, Goal? goal = null, Exception? ex = null) : base(message, ex) {
			this.goal = goal;
		}
	}
}
