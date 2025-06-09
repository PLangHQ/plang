using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Utils;

namespace PLang.Errors
{

	public record GroupedErrors(string Key = "GroupedErrors", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : IError
	{
		
		public string Message
		{
			get
			{
				string message = String.Empty;
				foreach (var error in ErrorChain)
				{
					message += error.Message + Environment.NewLine;
				}
				return message;
			}
		}
		public GoalStep? Step { get; set; }
		public Goal? Goal { get; set; }
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public List<IError> ErrorChain { get; set; } = new();
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
			if (contentType == "text")
			{

				string str = "";
				foreach (var error in ErrorChain)
				{
					str += $"\t- {error.Message}" + Environment.NewLine;
				}
				str += Environment.NewLine;
				foreach (var error in ErrorChain)
				{
					str += error.ToFormat() + Environment.NewLine;
				}
			}

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

		public int Count { get { return ErrorChain.Count; } }

		public Exception? Exception { get; }
	}

	public record MultipleError(IError InitialError, string Key = "MultipleError", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : IError
	{
		
		public string Message
		{
			get
			{
				string message = InitialError.Message + Environment.NewLine;
				foreach (var error in ErrorChain)
				{
					message += error.Message + Environment.NewLine;
				}
				return message;
			}
		}
		public GoalStep? Step { get; set; } = InitialError.Step;
		public Goal? Goal { get; set; } = InitialError.Step?.Goal;
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public List<IError> ErrorChain { get; set; } = new();
		public MultipleError Add(IError error)
		{
			if (error != InitialError)
			{
				ErrorChain.Add(error);
			}
			return this;
		}

		public object ToFormat(string contentType = "text")
		{
			if (contentType == "text")
			{
				string str = $@"{ErrorChain.Count + 1} errors occured:
	- {InitialError.Message}";
				str += $"\t- {InitialError.Message}" + Environment.NewLine;
				foreach (var error in ErrorChain)
				{
					str += $"\t- {error.Message}" + Environment.NewLine;
				}
				str += Environment.NewLine;
				str += InitialError.ToFormat() + Environment.NewLine;
				foreach (var error in ErrorChain)
				{
					str += error.ToFormat() + Environment.NewLine;
				}
			}

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

		public int Count { get { return ErrorChain.Count; } }

		public Exception? Exception { get; }
	}
}
