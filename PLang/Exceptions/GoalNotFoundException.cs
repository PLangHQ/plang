using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions
{
	public class GoalNotFoundException : Exception
	{
		public string AppPath { get; private set; }
		public string GoalName { get; private set; }
		public GoalNotFoundException(string message, string appPath, string goalName) : base(message) { 
			this.AppPath = appPath;
			this.GoalName = goalName;
		}
	}
}
