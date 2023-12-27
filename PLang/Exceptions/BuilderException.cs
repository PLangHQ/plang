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

	public class BuilderStepException : Exception
	{
		public GoalStep? GoalStep { get; set; }
		public BuilderStepException(string message, GoalStep? step = null) : base(message)
		{
			this.GoalStep = step;
		}
	}
}
