using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Events;
using PLang.Models;
using PLang.Utils;

namespace PLang.Errors.Events
{

    public record BuilderEventError(string Message, EventBinding? EventBinding = null, Goal? Goal = null, GoalStep? Step = null, string Key = "BuilderEvent", bool ContinueBuild = true, Exception? Exception = null, IError? InitialError = null) : Error(Message, Key, Exception: Exception), IEventError, IBuilderError
    {
        public bool IgnoreError => true;
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
            if (EventBinding != null)
            {
                str += Environment.NewLine + $" when calling event on {EventBinding.GoalToBindTo}.{EventBinding.EventType}.{EventBinding.EventScope}";
                str += Environment.NewLine + $" tried calling goal {EventBinding.GoalToCall}";
            }
            if (Step != null)
            {
                str += Environment.NewLine + $" in ({Step.LineNumber}) {Step.Text.MaxLength(80)} at {Goal.RelativeGoalPath}";
                str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";
			}
			else if (Goal != null)
			{
				str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";
			}

			if (Exception != null)
            {
                str += Environment.NewLine + Exception.ToString();
            }
            return str;
        }
    }
}
