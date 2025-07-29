using PLang.Building.Model;
using PLang.Models;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Errors
{
	public record ErrorHandled(IError Error) : Error(Error.Message), IErrorHandled
	{
		public bool IgnoreError => false;

		public IError? InitialError { get; } = Error;
	}

	public record Return(List<ObjectValue> ReturnVariables) : IError, IErrorHandled
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
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public List<IError> ErrorChain { get; set; } = new();

		public List<ObjectValue> Variables { get; set; } = new();
		public Exception? Exception => null;
		public string MessageOrDetail
		{
			get
			{
				return Message;
			}

		}

		public bool Handled { get; set; }

		public object AsData()
		{
			return this;
		}

		public object ToFormat(string contentType = "text")
		{
			return String.Empty;
		}
	}
}
