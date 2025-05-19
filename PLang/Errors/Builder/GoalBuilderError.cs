using PLang.Building.Model;

namespace PLang.Errors.Builder
{


	public record GoalBuilderError : BuilderError
	{
		public GoalBuilderError(IError error, Goal Goal, bool ContinueBuild = true) : base(error, ContinueBuild)
		{
			this.Goal = Goal;
		}

		public GoalBuilderError(string Message, Goal Goal, string Key = "GoalBuilder", int StatusCode = 400, bool ContinueBuild = true, 
					Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null, bool Retry = true) 
			: base(Message, Key, StatusCode, ContinueBuild, Exception, FixSuggestion, HelpfulLinks, Retry)
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
