using Newtonsoft.Json;

namespace PLang.Services.CompilerService
{
	public class ConditionImplementationResponse : ImplementationResponse
	{
		public ConditionImplementationResponse() { }
		public ConditionImplementationResponse(string name, string? implementation = null, string[]? @using = null, 
			string[]? assemblies = null, string? goalToCallOnTrue = null, string? goalToCallOnFalse = null, 
			Dictionary<string, object>? goalToCallOnTrueParameters = null, 
			Dictionary<string, object>? goalToCallOnFalseParameters = null)
		{
			Name = name;
			Implementation = implementation;
			Using = @using;
			Assemblies = assemblies;
			GoalToCallOnTrue = goalToCallOnTrue;
			GoalToCallOnFalse = goalToCallOnFalse;
			GoalToCallOnTrueParameters = goalToCallOnTrueParameters;
			GoalToCallOnFalseParameters = goalToCallOnFalseParameters;
		}

		public string? GoalToCallOnTrue { get; set; } = null;
		public Dictionary<string, object>? GoalToCallOnTrueParameters { get; set; } = null;
		public string? GoalToCallOnFalse { get; set; } = null;
		public Dictionary<string, object>? GoalToCallOnFalseParameters { get; set; } = null;
	}
}
