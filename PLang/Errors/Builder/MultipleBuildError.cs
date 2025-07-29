using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Builder
{
	public record GroupedBuildErrors(string Key = "GroupedBuildErrors", bool ContinueBuild = true, int StatusCode = 400, 
		string? FixSuggestion = null, string? HelpfulLinks = null) : GroupedErrors(Key, StatusCode, FixSuggestion, HelpfulLinks), IBuilderError
	{
		public bool Retry => true;
		public string? LlmBuilderHelp { get; set; }
		public new object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);		
		}
		public override string ToString()
		{
			string str = String.Empty;
			foreach (var error in ErrorChain)
			{
				if (error.Step == null && Step != null) error.Step = Step;
				if (error.Goal == null && Step != null) error.Goal = Step.Goal;
				
				str += error.ToFormat() + Environment.NewLine;
			}
			return str;
		}
		public string Message
		{
			get
			{
				return ErrorHelper.GetErrorMessageFromChain(this);
			}
		}
	}

	public record MultipleBuildError(IBuilderError InitialError, string Key = "MultipleBuildError", bool ContinueBuild = true, int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : MultipleError(InitialError, Key, StatusCode, FixSuggestion, HelpfulLinks), IBuilderError
	{
		public new IBuilderError InitialError {  get { return InitialError; } }
		public bool Retry => false;

		public string? LlmBuilderHelp { get; set; }
		public new object ToFormat(string contentType = "text")
		{
				return ErrorHelper.ToFormat(contentType, this);
			
		}
		public string Message
		{
			get
			{
				return ErrorHelper.GetErrorMessageFromChain(this);
			}
		}

		public override string ToString()
		{
			string str = String.Empty;
			foreach (var error in ErrorChain)
			{
				str += error.ToFormat() + Environment.NewLine;
			}
			return str;
		}
	}
}
