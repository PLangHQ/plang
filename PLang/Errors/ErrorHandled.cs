using PLang.Building.Model;
using PLang.Models;

namespace PLang.Errors
{
	public record ErrorHandled(IError Error) : Error(Error.Message), IErrorHandled
	{
		public bool IgnoreError => false;

		public IError? InitialError { get; } = Error;
	}

	public record Return(ReturnDictionary<string, object?> Variables) : IError, IErrorHandled
	{
		public bool IgnoreError => true;

		public IError? InitialError { get; } = null;

		public int StatusCode => 200;

		public string Key => "Return";

		public string Message => String.Empty;

		public string? FixSuggestion => String.Empty;

		public string? HelpfulLinks => String.Empty;

		public GoalStep? Step { get;set; }
		public Goal? Goal { get; set; }

		public Exception? Exception => null;

		public object ToFormat(string contentType = "text")
		{
			return String.Empty;
		}
	}
}
