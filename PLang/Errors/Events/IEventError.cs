
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Errors.Events
{
	public interface IEventError : IError
	{
		bool IgnoreError { get; }
		IError? InitialError { get; }
	}

	public record HandledEventError(IError InitialError, int StatusCode, string Key, string Message, Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null) : IEventError, IErrorHandled
	{
		public string Id { get; } = Guid.NewGuid().ToString();
		public GoalStep? Step { get; set; }
		public Goal Goal { get; set; }

		public bool IgnoreError => false;
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public List<IError> ErrorChain { get; set; } = new();

		public List<ObjectValue>? Variables { get; set; }
		public object AsData()
		{
			throw new NotImplementedException();
		}
		public string MessageOrDetail
		{
			get
			{
				AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool isEnabled);
				if (isEnabled)
				{
					return ToString();
				}
				else
				{
					return Message.MaxLength(80);
				}
			}

		}

		[IgnoreWhenInstructed]
		public bool Handled { get; set; }
		public object ToFormat(string contentType = "text")
		{
			return this.ToFormat(contentType);
		}
	}
}
