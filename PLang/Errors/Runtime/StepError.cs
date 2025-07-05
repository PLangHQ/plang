using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Runtime
{

	public record StepError : GoalError {
		public StepError(string Message, GoalStep Step, string Key = "StepError", int StatusCode = 400,
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) 
			: base(Message, Step?.Goal, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
			this.Step = Step;
			this.Goal = Step?.Goal;	
		}
	

		public override GoalStep Step { get; set; }
		public override Goal? Goal { get; set; }
		public override string ToString()
		{
			return base.ToString(); 
		}
	}
}
