
namespace PLang.Services.CompilerService
{
	public class Implementation
	{
		public Implementation(string name, string code, string[]? @using, Dictionary<string, string> inputParameters, 
			Dictionary<string, ParameterType>? outParameterDefinition, string? goalToCallOnTrue, string? goalToCallOnFalse,
			Dictionary<string, object>? goalToCallOnTrueParameters = null,
			Dictionary<string, object>? goalToCallOnFalseParameters = null)
		{
			Name = name;
			Code = code;
			Using = @using;
			InputParameters = inputParameters;
			OutParameterDefinition = outParameterDefinition;
			GoalToCallOnTrue = goalToCallOnTrue;
			GoalToCallOnFalse = goalToCallOnFalse;
			GoalToCallOnTrueParameters = goalToCallOnTrueParameters;
			GoalToCallOnFalseParameters = goalToCallOnFalseParameters;
		}

		public string Name { get; private set; }
		public string Code { get; private set; }
		public string[]? Using { get; private set; }
		public Dictionary<string, string> InputParameters { get; private set; }
		public Dictionary<string, ParameterType>? OutParameterDefinition { get; private set; }
		public string? GoalToCallOnTrue { get; private set; }
		public string? GoalToCallOnFalse { get; private set; }
		public Dictionary<string, object?>? GoalToCallOnTrueParameters { get; set; } = null;
		public Dictionary<string, object?>? GoalToCallOnFalseParameters { get; set; } = null;
	}
}
