﻿using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Builder
{
	public record BuilderError(string Message, string Key = "Builder", int StatusCode = 400, bool ContinueBuild = true, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) : Error(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks), IBuilderError
	{
		public override object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}
		public override string ToString()
		{
			return base.ToString();
		}
	}
}
