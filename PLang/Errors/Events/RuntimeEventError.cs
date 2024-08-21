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
		public new object ToFormat(string contentType = "text")
		{
            string str = string.Empty;
            if (Key != null)
            {
                str += "[" + Key + "] ";
            }
            str += Message;
            str += Environment.NewLine + $" when calling '{EventBinding.EventType}.{EventBinding.EventScope}' event on '{EventBinding.GoalToBindTo}'";
            str += Environment.NewLine + $" tried calling goal {EventBinding.GoalToCall}";
            if (Step != null)
            {
                str += Environment.NewLine + $" in ({Step.LineNumber}) {Step.Text.MaxLength(80)} at {Step.Goal.RelativeGoalPath}";
                str += Environment.NewLine + $" in {Step.Goal.GoalName} at {Step.Goal.RelativeGoalPath}";
            } else if (Goal != null)
            {
				str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";
			}

            if (Exception != null)
            {
                var lowestException = ExceptionHelper.GetLowestException(Exception);
                str += Environment.NewLine + lowestException.ToString();
            }
            return str;
        }
    }
}
