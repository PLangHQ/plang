namespace PLang.Errors.Events
{
	public interface IEventError : IError
	{
		bool IgnoreError { get; }
		IError? InitialError { get; }
	}

	public record HandledEventError(IError InitialError, int StatusCode, string Key, string Message, string? FixSuggestion = null, string? HelpfulLinks = null) : IEventError, IErrorHandled
	{
		public bool IgnoreError => false;

		public object ToFormat(string contentType = "text")
		{
			return InitialError.ToFormat(contentType);
		}
	}
}
