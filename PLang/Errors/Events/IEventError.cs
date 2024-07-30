﻿
using PLang.Building.Model;

namespace PLang.Errors.Events
{
	public interface IEventError : IError
	{
		bool IgnoreError { get; }
		IError? InitialError { get; }
	}

	public record HandledEventError(IError InitialError, int StatusCode, string Key, string Message, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) : IEventError, IErrorHandled
	{
		public GoalStep? Step { get; set; }
		public Goal Goal { get; set; }

		public bool IgnoreError => false;

		public object ToFormat(string contentType = "text")
		{
			return InitialError.ToFormat(contentType);
		}
	}
}
