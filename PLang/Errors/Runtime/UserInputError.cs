using PLang.Building.Model;
using PLang.Errors.Events;
using PLang.Errors.Interfaces;
using PLang.Events;

namespace PLang.Errors.Runtime
{
	public record UserInputError(string Message, GoalStep Step, string Key = "UserDefinedError", int StatusCode = 400,
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null)
			: StepError(Message, Step, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks), IUserInputError, IEventError
	{
		public bool IgnoreError => false;

		public IError? InitialError => null;
		public override GoalStep Step { get; set; } = Step;
		public override Goal? Goal { get; set; } = Step.Goal;
		public override string ToString()
		{
			return Message; 
		}
	}
}
