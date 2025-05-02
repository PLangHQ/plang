using NBitcoin.Protocol;
using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors.Builder
{
	public record BuilderError : Error, IBuilderError
	{


		public BuilderError(IError error, bool ContinueBuild = true)
		: this(error.Message, error.Key, error.StatusCode, ContinueBuild, error.Exception, error.FixSuggestion, error.HelpfulLinks)
		{
			if (error is IBuilderError be && ContinueBuild)
			{
				this.ContinueBuild = be.ContinueBuild;
			}
		}

		public BuilderError(string Message, string Key = "Builder", int StatusCode = 400, bool ContinueBuild = true,
			Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null, bool Retry = false) :
				base(Message, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
			this.ContinueBuild = ContinueBuild;
			this.Retry = Retry;
		}

		public bool ContinueBuild { get; init; }

		public bool Retry {get;init;}

		public override object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}
		public override string ToString() 
		{
			return ErrorHelper.ToFormat("text", this).ToString();
		}
	}
}
