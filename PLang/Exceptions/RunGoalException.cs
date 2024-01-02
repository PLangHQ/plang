using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class RunGoalException : Exception
	{
		public RunGoalException(string goalName, Exception ex) : base(goalName, ex) { }
	}
}
