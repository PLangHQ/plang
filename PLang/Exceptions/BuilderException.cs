using PLang.Building.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class BuilderException : Exception
	{
		public Goal? Goal { get; set; }
		public BuilderException(string message, Goal? goal = null) : base(message)
		{
			this.Goal = goal;
		}
	
	}

	public class BuilderStepException : BaseStepException
	{
		public BuilderStepException(string message, GoalStep? step = null, Exception? innerException = null) : base(step, message, innerException)
		{
		}
	}
}
