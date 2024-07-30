using PLang.Building.Model;
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
		public Exception? Exception { get; }
		public object ToFormat(string contentType = "text");
	}
	public record Error(string Message, string Key = "GeneralError", int StatusCode = 400, Exception? Exception = null,
		string? FixSuggestion = null, string? HelpfulLinks = null) : IError
	{
		public virtual GoalStep? Step { get; set; }
		public virtual Goal? Goal { get; set; }

		public virtual object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}

		public override string ToString()
		{
			return ErrorHelper.ToFormat("text", this).ToString();
		}
	}

	public interface IErrorHandled { }


	public record EndGoal(GoalStep Step, string Message, int StatusCode  = 200) : StepError(Message, Step, "EndGoal", StatusCode), IErrorHandled;
}
