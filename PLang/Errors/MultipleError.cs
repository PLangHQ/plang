using PLang.Utils;

namespace PLang.Errors
{
	public record MultipleError(string Key = "MultipleError", int StatusCode = 400, string? FixSuggestion = null, string? HelpfulLinks = null) : IError
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

		public object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}

		public List<IError> Errors { get { return errors; } }


		public int Count { get { return errors.Count; } }
	}
}
