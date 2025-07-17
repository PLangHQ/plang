using PLang.Building.Model;

namespace PLang.Errors.Runtime
{
	public record GoalError : Error {
		public GoalError(string Message, Goal Goal, string Key = "GoalError", int StatusCode = 400,
		Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null)
		: base(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
			this.Goal = Goal;
		}
		public override Goal? Goal { get; set; }
		public override string ToString()
		{
			return base.ToString(); 
		}
	}
}
