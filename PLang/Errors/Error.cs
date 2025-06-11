using PLang.Building.Model;
using PLang.Errors.Events;
using PLang.Errors.Runtime;
using PLang.Utils;

namespace PLang.Errors
{
	public interface IError
	{
		public int StatusCode { get; }
		public string Key { get; }
		public string Message { get; }
		public string? FixSuggestion { get; }
		public string? HelpfulLinks { get; }
		public GoalStep? Step { get; set; }
		public Goal? Goal { get; set; }
		public DateTime CreatedUtc { get; init; }
		public Exception? Exception { get; }
		public List<IError> ErrorChain { get; set; }
		public string MessageOrDetail { get; }
		public object ToFormat(string contentType = "text");
		public object AsData();
	}
	public record Error(object error) : IError
	{
		public Error(string Message, string Key = "GeneralError", int StatusCode = 400, Exception? Exception = null,
		string? FixSuggestion = null, string? HelpfulLinks = null, object? Data = null, Dictionary<string, object?>? Properties = null)
			: this(new
			{
				Message,
				Key,
				StatusCode,
				Properties,
				Data,
				Exception,
				FixSuggestion,
				HelpfulLinks,
			})
		{
			this.Message = Message;
			this.Key = Key;
			this.StatusCode = StatusCode;	
			this.Exception = Exception;
			this.FixSuggestion = FixSuggestion;
			this.HelpfulLinks = HelpfulLinks;
		}
		//public Error(IErrorReporting error)
		public virtual GoalStep? Step { get; set; }
		public virtual Goal? Goal { get; set; }
		public string? FixSuggestion { get; set; }
		public string? HelpfulLinks { get; set; }
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public virtual object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}

		public List<IError> ErrorChain { get; set; } = new();

		public int StatusCode {get;set;}

		public string Key { get; set; }

		public string Message { get; set; }

		public Exception? Exception { get; set; }

		public string MessageOrDetail {
			get
			{
				AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool isEnabled);
				if (isEnabled)
				{
					return ToString();
				} else
				{
					return Message.MaxLength(80);
				}
			}
			
		}

		public override string? ToString()
		{
			return ErrorHelper.ToFormat("text", this).ToString();
		}
		public virtual object AsData()
		{
			return this;
		}


	}

	public interface IErrorHandled : IEventError, IError { }


	public record EndGoal(GoalStep Step, string Message, int StatusCode  = 200, int Levels = 0) : StepError(Message, Step, "EndGoal", StatusCode), IErrorHandled
	{
		public override GoalStep? Step { get; set; } = Step;
		public override Goal? Goal { get; set; } = Step.Goal;
		public int Levels { get; set; } = Levels;

		public bool IgnoreError => false;

		public IError? InitialError { get; } = null;
	}
}
