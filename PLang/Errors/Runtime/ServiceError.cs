using PLang.Errors.Builder;

namespace PLang.Errors.Runtime
{
	public record ServiceError : Error, IBuilderError
	{
		public ServiceError(string Message, Type Type, string Key = "ServiceError", int StatusCode = 400,
			bool ContinueBuild = true, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) :
		base(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{

		}
		public bool Retry => false;
		public string? LlmBuilderHelp { get; set; }

		public bool ContinueBuild => true;

		public override string ToString()
		{
			return base.ToString();
		}
	}
}
