using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors
{
	public record GroupedErrors(string Key = "GroupedErrors", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : IError
	{
		protected List<IError> errors = new List<IError>();
		public string Message
		{
			get
			{
				string message = String.Empty;
				foreach (var error in errors)
				{
					message += error.Message + Environment.NewLine;
				}
				return message;
			}
		}
		public GoalStep? Step { get; set; }
		public Goal? Goal { get; set; }
		public void Add(IError error)
		{
			errors.Add(error);
			if (errors.Count == 1)
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
				foreach (var error in errors)
				{
					str += $"\t- {error.Message}" + Environment.NewLine;
				}
				str += Environment.NewLine;
				foreach (var error in errors)
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

		public List<IError> Errors { get { return errors; } }


		public int Count { get { return errors.Count; } }

		public Exception? Exception { get; }
	}

	public record MultipleError(IError InitialError, string Key = "MultipleError", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : IError
	{
		protected List<IError> errors = new List<IError>();
		public string Message
		{
			get
			{
				string message = InitialError.Message + Environment.NewLine;
				foreach (var error in errors)
				{
					message += error.Message + Environment.NewLine;
				}
				return message;
			}
		}
		public GoalStep? Step { get; set; } = InitialError.Step;
		public Goal? Goal { get; set; } = InitialError.Step?.Goal;
		public void Add(IError error)
		{
			if (error != InitialError)
			{
				errors.Add(error);
			}
		}

		public object ToFormat(string contentType = "text")
		{
			if (contentType == "text")
			{
				string str = $@"{errors.Count + 1} errors occured:
	- {InitialError.Message}";
				str += $"\t- {InitialError.Message}" + Environment.NewLine;
				foreach (var error in errors)
				{
					str += $"\t- {error.Message}" + Environment.NewLine;
				}
				str += Environment.NewLine;
				str += InitialError.ToFormat() + Environment.NewLine;
				foreach (var error in errors)
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

		public List<IError> Errors { get { return errors; } }


		public int Count { get { return errors.Count; } }

		public Exception? Exception { get; }
	}
}
