using PLang.Building.Model;
using PLang.Events;
using PLang.Utils;

namespace PLang.Errors.Events
{

	public record RuntimeEventError(string Message, EventBinding EventBinding, Goal? Goal = null, GoalStep? Step = null, string Key = "RuntimeEvent", Exception? Exception = null, IError? InitialError = null) : Error(Message, Key, Exception: Exception), IEventError, IError
    {
        public bool IgnoreError => false;
		public override string ToString()
		{
			return ToFormat().ToString();
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
