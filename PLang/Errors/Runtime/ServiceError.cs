using PLang.Errors.Builder;

namespace PLang.Errors.Runtime
{
	public record ServiceError(string Message, Type Type, string Key = "ServiceError", int StatusCode = 400, bool ContinueBuild = true, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) : 
		Error(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks), IBuilderError
	{
		public bool Retry => false;
		public override string ToString()
		{
			return base.ToString(); 
		}
	}
}
