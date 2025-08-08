using PLang.Building.Model;
using PLang.Errors.Runtime;

namespace PLang.Errors
{
	public record AssertError : StepError
	{


		public AssertError(string Message, object ExpectedValue, object ActualValue, GoalStep Step, string Key = "AssertError", int StatusCode = 500,
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null)
			: base(Message, Step, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
			this.ExpectedValue = ExpectedValue;
			this.ActualValue = ActualValue;
		}

		public object? ExpectedValue { get; set; }
		public object? ActualValue { get; set; }
	}
}
