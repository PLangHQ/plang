namespace PLang.Errors.Builder
{
	public record MultipleBuildError(string Key = "MultipleBuildError", bool ContinueBuild = true, int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : MultipleError(Key, StatusCode, FixSuggestion, HelpfulLinks), IBuilderError
	{
		private List<IError> errors = new List<IError>();
		public string Message
		{
			get
			{
				string message = String.Empty;
				foreach (var error in errors)
				{
					message += error.Message + Environment.NewLine;
				}
				return message;
			}
		}

		public void Add(IError error)
		{
			errors.Add(error);
		}
		public List<IError> Errors { get { return errors; } }

		public new object ToFormat(string contentType = "text")
		{
			string str = String.Empty;
			foreach (var error in errors)
			{
				str += error.ToFormat() + Environment.NewLine;
			}
			return str;
		}

		public new int Count { get { return errors.Count; } }
	}
}
