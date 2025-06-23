using PLang.Building;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace PLang.Errors
{
	public record ExceptionError : IError, IBuilderError
	{

		public static readonly string UnhandledError = "UnhandledError";
		public bool Retry { get; set; }
		public ExceptionError(IError error)
		{

			this.StatusCode = error.StatusCode;
			this.Key = error.Key;
			this.Message = error.Message;
			this.FixSuggestion = error.FixSuggestion;
			this.HelpfulLinks = error.HelpfulLinks;
			this.Goal = error.Goal;
			this.Step = error.Step;
		}
		public ExceptionError(Exception ex, string? Message = null, Goal? Goal = null, GoalStep? Step = null, int StatusCode = 500, string Key = "UnhandledError", string? FixSuggestion = null, string? HelpfulLinks = null, bool Retry = true) { 
		
			var lowestException = ExceptionHelper.GetLowestException(ex);
			this.StatusCode = StatusCode;
			this.Key = Key;
			this.Message = Message ?? lowestException.Message;
			this.Exception = lowestException ?? ex;
			this.FixSuggestion = FixSuggestion;
			this.HelpfulLinks = HelpfulLinks;
			this.Goal = Goal;
			this.Step = Step;
			this.Retry = Retry;
		}

		public int StatusCode { get; init; }

		public string Key {get;init;}

		public string Message { get; init; }

		public Exception Exception { get; init; }
		public string? FixSuggestion { get; init; }
		public string? HelpfulLinks { get; init; }
		public string? LlmBuilderHelp { get; set; }
		public bool ContinueBuild => false;
		public Goal? Goal { get; set; }
		public GoalStep? Step { get; set; }
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public List<IError>? ErrorChain { get; set; }
		public List<ObjectValue> Variables { get; set; } = new();
		public object AsData()
		{
			throw new NotImplementedException();
		}
		public string MessageOrDetail
		{
			get
			{
				AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool isEnabled);
				if (isEnabled)
				{
					return ToString();
				}
				else
				{
					return Message.MaxLength(80);
				}
			}

		}
		public object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}
		public override string ToString()
		{
			return ToFormat().ToString();
		}
	}
}
