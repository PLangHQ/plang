using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Builder
{
	public record StepBuilderError : GoalBuilderError
	{
		public StepBuilderError(string Message, GoalStep Step, string Key = "StepBuilder", int StatusCode = 400,
										bool ContinueBuild = true, Exception? ex = null, string? FixSuggestion = null, 
										string? HelpfulLinks = null, bool Retry = false)
										: base(Message, Step.Goal, Key, StatusCode, ContinueBuild, ex, FixSuggestion, HelpfulLinks, Retry)
		{
			this.Step = Step;
			this.Goal = Step.Goal;
		}


		public StepBuilderError(IError error, GoalStep Step, bool ContinueBuild = true) : base(error, Step.Goal, ContinueBuild)
		{
			this.Step = Step;
			this.Goal = Step.Goal;
		}


		public override GoalStep Step { get; set; }
		public override Goal Goal { get; set; }

		public override string ToString()
		{
			return base.ToString();
		}
	}

}
