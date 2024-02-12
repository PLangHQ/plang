using Newtonsoft.Json;
using PLang.Building.Model;
using static PLang.Modules.BaseBuilder;

namespace PLang.Exceptions
{
	public class RuntimeProgramException : Exception
	{

		public GoalStep Step { get; set; }
		public Dictionary<string, object?> ParameterValues { get; }
		public GenericFunction GenericFunction { get; }

		public RuntimeProgramException(string message, GoalStep step, GenericFunction genericFunction, Dictionary<string, object?> parameterValues, Exception? ex = null) : base(message, ex)
		{
			this.Step = step;
			ParameterValues = parameterValues;
			GenericFunction = genericFunction;
		}

		public override string ToString()
		{
			return $@"

Error happend at
	Step: {Step.Text} line {Step.LineNumber}
	Goal: {Step.Goal.GoalName} in {Step.Goal.GoalFileName}

	Calling {Step.ModuleType}.Program.{GenericFunction.FunctionName} 
		Parameters:
			{JsonConvert.SerializeObject(GenericFunction.Parameters)} 

		Parameter values:
{JsonConvert.SerializeObject(ParameterValues)}

		Return value {JsonConvert.SerializeObject(GenericFunction.ReturnValue)}";
		}
	}
}
