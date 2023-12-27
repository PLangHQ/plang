using PLang.Building.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class ParameterException : Exception
	{
		public GoalStep? Step { get; set; }
		public ParameterException(string message, GoalStep? step, Exception? ex = null) : base(message, ex) {
			this.Step = step;
		}
	}
}
