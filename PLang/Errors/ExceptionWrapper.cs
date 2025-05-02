using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Errors
{
	public class ExceptionWrapper : Exception, IError, IBuilderError
	{
		public bool Retry => false;
		public ExceptionWrapper(IError error) : base(error.Message)
		{

			this.StatusCode = error.StatusCode;
			this.Key = error.Key;
			this.Message = error.Message;
			this.FixSuggestion = error.FixSuggestion;
			this.HelpfulLinks = error.HelpfulLinks;
			this.Goal = error.Goal;
			this.Step = error.Step;
		}
		public int StatusCode { get; init; }

		public string Key { get; init; }

		public string Message { get; init; }

		public Exception Exception { get; init; }
		public string? FixSuggestion { get; init; }
		public string? HelpfulLinks { get; init; }

		public bool ContinueBuild => false;
		public Goal? Goal { get; set; }
		public GoalStep? Step { get; set; }

		public object AsData()
		{
			throw new NotImplementedException();
		}

		public object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}
	}
}
