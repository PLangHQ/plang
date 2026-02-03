using NBitcoin.Protocol;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Errors.Interfaces;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Errors
{
	public record GroupedUserInputErrors(string Key = "GroupedErrors", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) :
		GroupedErrors(Key, StatusCode, FixSuggestion, HelpfulLinks), IUserInputError
	{
		public override string ToString()
		{
			return Message;
		}

	}


	public record GroupedErrors(string Key = "GroupedErrors", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : IError
	{
		public string Id { get; } = Guid.NewGuid().ToString();
		public string Message
		{
			get
			{
				return ErrorHelper.GetErrorMessageFromChain(this);

			}
		}

		public string FixSuggestion
		{
			get
			{
				string message = "";
				foreach (var error in ErrorChain)
				{
					message += $"\t- {error.FixSuggestion}\n";
				}

				if (Step != null)
				{
					message += $"\t\t - at {Step.RelativeGoalPath}:{Step.LineNumber}";
				}

				return message;
			}
		}
		public GoalStep? Step { get; set; }
		public Goal? Goal { get; set; }
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public List<IError> ErrorChain { get; set; } = new();

		[IgnoreWhenInstructed]
		public bool Handled { get; set; }
		public List<ObjectValue> Variables { get; set; } = new();
		
		public void Add(IError error)
		{
			if (ErrorChain == null) ErrorChain = new();
			if (error is GroupedBuildErrors groupedBuildErrors)
			{
				ErrorChain.AddRange(groupedBuildErrors.ErrorChain);
			}
			else
			{
				ErrorChain.Add(error);
			}
			if (ErrorChain.Count == 1)
			{
				Step = error.Step;
				Goal = error.Goal;
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
		public int Count { get { return ErrorChain.Count; } }

		public Exception? Exception { get; }
	}
}
