using Newtonsoft.Json;
using PLang.Models;

namespace PLang.Services.CompilerService
{
	public class ConditionImplementationResponse : ImplementationResponse
	{
		public ConditionImplementationResponse() { }
		public ConditionImplementationResponse(string @namespace, string name, string? implementation = null,
			string[]? inputParameters = null, string[]? @using = null, 
			string[]? assemblies = null, GoalToCall? goalToCallOnTrue = null, GoalToCall? goalToCallOnFalse = null, 
			Dictionary<string, object>? goalToCallOnTrueParameters = null, 
			Dictionary<string, object>? goalToCallOnFalseParameters = null)
		{
			Namespace = @namespace;
			Name = name;
			Implementation = implementation;
			InputParameters = inputParameters;
			Using = @using;
			Assemblies = assemblies;
			GoalToCallOnTrue = goalToCallOnTrue;
			GoalToCallOnFalse = goalToCallOnFalse;
			GoalToCallOnTrueParameters = goalToCallOnTrueParameters;
			GoalToCallOnFalseParameters = goalToCallOnFalseParameters;
			
		}

		public GoalToCall? GoalToCallOnTrue { get; set; } = null;
		public Dictionary<string, object?>? GoalToCallOnTrueParameters { get; set; } = null;
		public GoalToCall? GoalToCallOnFalse { get; set; } = null;
		public Dictionary<string, object?>? GoalToCallOnFalseParameters { get; set; } = null;
	}
}
