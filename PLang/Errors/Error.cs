using Nethereum.Contracts;
using Newtonsoft.Json;
using Org.BouncyCastle.Security;
using PLang.Building.Model;
using PLang.Events;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors
{
	public interface IError
	{
		public string? Key { get; }
		public string Message { get; }
		public string ToString();
	}
	public record Error(string Message, string? Key = null, Exception? Exception = null) : IError
	{
		public override string ToString()
		{
			string str = String.Empty;
			if (Key != null)
			{
				str += "[" + Key + "] ";
			}
			str += Message;
			if (Exception != null)
			{
				str += Environment.NewLine + Exception.ToString();
			}
			return str;
		}
	}

	public record MultipleError(string? Key = null) : IError
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
	}

	public record GoalError(string Message, Goal Goal, string? Key = null, Exception? Exception = null) : Error(Message, Key, Exception)
	{
		public override string ToString()
		{
			string str = String.Empty;
			if (Key != null)
			{
				str += "[" + Key + "] ";
			}
			str += Message;
			str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";

			if (Exception != null)
			{
				str += Environment.NewLine + Exception.ToString();
			}
			return str;
		}
	}

	public record StepError(string Message, GoalStep Step, string? Key = null, Exception? Exception = null) : GoalError(Message, Step.Goal, Key, Exception)
	{
		public override string ToString()
		{
			string str = String.Empty;
			if (Key != null)
			{
				str += "[" + Key + "] ";
			}
			str += Message;
			str += Environment.NewLine + $" in ({Step.LineNumber}) {Step.Text.MaxLength(80)} at {Goal.RelativeGoalPath}";
			str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";

			if (Exception != null)
			{
				str += Environment.NewLine + Exception.ToString();
			}
			return str;
		}
	}


	public record ProgramError(string Message, GoalStep Step, string ModuleName, string FunctionName, List<Parameter> Parameters,
		List<ReturnValue>? ReturnValue = null, Dictionary<string, object?>? ParameterValues = null,
		string? Key = null, Exception? Exception = null) : StepError(Message, Step, Key, Exception)
	{
		public override string ToString()
		{
			string str = String.Empty;
			if (Key != null)
			{
				str += "[" + Key + "] ";
			}
			str += Message;
			str += Environment.NewLine + $" when calling {ModuleName}.{FunctionName}";
			str += Environment.NewLine + $" in ({Step.LineNumber}) {Step.Text.MaxLength(80)} at {Goal.RelativeGoalPath}";
			str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";
			str += Environment.NewLine + $" parameters: {JsonConvert.SerializeObject(Parameters)}";
			str += Environment.NewLine + $" return value: {JsonConvert.SerializeObject(ReturnValue)}";
			if (ParameterValues != null)
			{
				str += Environment.NewLine + $" values: {JsonConvert.SerializeObject(ParameterValues)}";
			}
			if (Exception != null)
			{
				str += Environment.NewLine + Exception.ToString();
			}
			return str;
		}
	}

	public record EventError(string Message, EventBinding EventBinding, Goal? Goal, GoalStep? Step, string? Key, Exception? Exception) : Error(Message, Key, Exception)
	{

		public override string ToString()
		{
			string str = String.Empty;
			if (Key != null)
			{
				str += "[" + Key + "] ";
			}
			str += Message;
			str += Environment.NewLine + $" when calling event on {EventBinding.GoalToBindTo}.{EventBinding.EventType}.{EventBinding.EventScope}";
			str += Environment.NewLine + $" tried calling goal {EventBinding.GoalToCall}";
			if (Step != null && Goal != null)
			{
				str += Environment.NewLine + $" in ({Step.LineNumber}) {Step.Text.MaxLength(80)} at {Goal.RelativeGoalPath}";
				str += Environment.NewLine + $" in {Goal.GoalName} at {Goal.RelativeGoalPath}";
			}

			if (Exception != null)
			{
				str += Environment.NewLine + Exception.ToString();
			}
			return str;
		}
	}

	public record ErrorHandled(IError Error) : Error(Error.Message);

	public record EndGoal(GoalStep Step, string Message) : StepError(Message, Step);
}
