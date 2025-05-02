using PLang.Building.Model;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors
{
	public class CancelledError : IError
	{
		private Goal goal;
		private GoalStep step;
		private readonly GenericFunction function;

		public CancelledError(Goal goal, GoalStep step, GenericFunction function)
		{
			this.goal = goal;
			this.step = step;
			this.function = function;
		}
		public int StatusCode => 9001;

		public string Key => "CancelledError";

		public string Message => "Task was cancelled";

		public string? FixSuggestion => null;

		public string? HelpfulLinks => null;

		public GoalStep? Step { get => step; set { step = value; } }
		public Goal? Goal { get => goal; set { goal = value; } }
		public GenericFunction? GenericFunction { get => function; }

		public Exception? Exception => null;

		public object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}

		public override string? ToString()
		{
			return ErrorHelper.ToFormat("text", this).ToString();
		}

		public object AsData()
		{
			throw new NotImplementedException();
		}
	}
}
