using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Exceptions
{
	public class RuntimeProgramException : Exception
	{
		public int StatusCode { get; }
		public string Type { get; }
		public GoalStep? Step { get; set; }
		public Dictionary<string, object?>? ParameterValues { get; }
		public MethodExecution? MethodExecution { get; }

		public RuntimeProgramException(string message, int statusCode, string type, GoalStep? step) : base(message)
		{
			StatusCode = statusCode;
			Type = type;
			Step = step;
		}
		public RuntimeProgramException(string message, int statusCode, string type, GoalStep step, MethodExecution genericFunction, Dictionary<string, object?> parameterValues, Exception? ex = null) : base(message, ex)
		{
			this.Step = step;
			ParameterValues = parameterValues;
			MethodExecution = genericFunction;
			StatusCode = statusCode;
			Type = type;
		}

		public override string ToString()
		{
			string innerEx = "";
			var ex = this.InnerException;
			while (ex != null)
			{
				innerEx += $@"
------
Message: {ex.Message}
StackTrace: {ex.StackTrace}
------
";
				ex = ex.InnerException;
			}
			string error = Message;
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);
			if (!isDebug)
			{
				AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out isDebug);
			}

			if (isDebug && Step != null && MethodExecution != null)
			{
				error += $@"

Error happend at
	Step: {Step.Text} line {Step.LineNumber}
	Goal: {Step.Goal.GoalName} in {Step.Goal.GoalFileName}

	Calling {Step.ModuleType}.Program.{MethodExecution.FunctionName} 
		Parameters:
			{JsonConvert.SerializeObject(MethodExecution.Parameters)} 

		Parameter values:
{JsonConvert.SerializeObject(ParameterValues)}

		Return value {JsonConvert.SerializeObject(MethodExecution.ReturnValues)}
	
	------
	Exception: {this.Message}
	StackTrace: {this.StackTrace}
	{innerEx}";
			}
			return error;
		}
	}
}
