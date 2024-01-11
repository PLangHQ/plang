using PLang.Building.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class RuntimeStepException : Exception
	{
		public GoalStep Step { get; set; }
		public RuntimeStepException(string message, GoalStep step) : base(message)
		{
			this.Step = step;
		}
		public RuntimeStepException(GoalStep step, Exception ex) : base($"Step '{step.Text}' had exception", ex)
		{
			this.Step = step;
		}
	}
}
