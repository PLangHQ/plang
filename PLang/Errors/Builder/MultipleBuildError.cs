using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Builder
{
	public record GroupedBuildErrors(string Key = "GroupedBuildErrors", bool ContinueBuild = true, int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : GroupedErrors(Key, StatusCode, FixSuggestion, HelpfulLinks), IBuilderError
	{
		public bool Retry => false;
		public new object ToFormat(string contentType = "text")
		{
			if (contentType == "text")
			{
				string str = String.Empty;
				foreach (var error in errors)
				{
					str += error.ToFormat() + Environment.NewLine;
				}
				return str;
			}
			else
			{
				return ErrorHelper.ToFormat(contentType, this);
			}
		}
		public override string ToString()
		{
			string str = String.Empty;
			foreach (var error in errors)
			{
				str += error.ToFormat() + Environment.NewLine;
			}
			return str;
		}

	}

	public record MultipleBuildError(IBuilderError InitialError, string Key = "MultipleBuildError", bool ContinueBuild = true, int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : MultipleError(InitialError, Key, StatusCode, FixSuggestion, HelpfulLinks), IBuilderError
	{
		public new IBuilderError InitialError {  get { return InitialError; } }
		public bool Retry => false;
		public new object ToFormat(string contentType = "text")
		{
			if (contentType == "text")
			{
				string str = String.Empty;
				foreach (var error in errors)
				{
					str += error.ToFormat() + Environment.NewLine;
				}
				return str;
			} else
			{
				return ErrorHelper.ToFormat(contentType, this);
			}
		}
		public override string ToString()
		{
			string str = String.Empty;
			foreach (var error in errors)
			{
				str += error.ToFormat() + Environment.NewLine;
			}
			return str;
		}
	}
}
