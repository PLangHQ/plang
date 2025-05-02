using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Events;
using PLang.Models;
using PLang.Utils;

namespace PLang.Errors.Events
{

	public record BuilderEventError(string Message, EventBinding? EventBinding = null, Goal? Goal = null, GoalStep? Step = null,
		string Key = "BuilderEvent", bool ContinueBuild = true,
		Exception? Exception = null, IError? InitialError = null,
		string? FixSuggestion = null, string? HelpfulLinks = null) : Error(Message, Key, Exception: Exception, FixSuggestion: FixSuggestion, HelpfulLinks: HelpfulLinks), IEventError, IBuilderError
	{
		public bool IgnoreError => true;
		public bool Retry => false;
		public override string ToString()
		{
			return (InitialError ?? this).ToFormat().ToString();
		}
		public override GoalStep? Step { get; set; } = Step;
		public override Goal? Goal { get; set; } = Goal;

		public new object ToFormat(string contentType = "text")
		{


			string str = string.Empty;
			if (EventBinding != null)
			{
				if (string.IsNullOrWhiteSpace(EventBinding.GoalToBindTo) && EventBinding.EventScope == EventScope.Goal)
				{
					str += Environment.NewLine + " Could not determine what goal to bind to";
				}
				if (string.IsNullOrWhiteSpace(EventBinding.EventType))
				{
					str += Environment.NewLine + " Could not determine event type, is it before or after execution";
				}
				if (string.IsNullOrWhiteSpace(EventBinding.EventScope))
				{
					var eventScopes = TypeHelper.GetStaticFields(typeof(EventScope));
					str += Environment.NewLine + $" Could not determine event scope. These scopes are available {string.Join(", ", eventScopes)}";
				}
				if (string.IsNullOrWhiteSpace(EventBinding.GoalToCall))
				{
					str += Environment.NewLine + " Could not determine goal to call on the event";
				}
			}

			return ErrorHelper.ToFormat(contentType, InitialError ?? this, extraInfo: str);

		}
	}
}
