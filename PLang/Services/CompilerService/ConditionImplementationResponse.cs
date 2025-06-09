using Newtonsoft.Json;
using PLang.Models;
using static PLang.Modules.BaseBuilder;

namespace PLang.Services.CompilerService
{
	public record ConditionImplementationResponse : ImplementationResponse
	{
		public ConditionImplementationResponse() { }
		public ConditionImplementationResponse(string reasoning, string @namespace, string name, string? implementation = null,
			List<Parameter>? parameters = null, List<string>? @using = null, 
			List<string>? assemblies = null, GoalToCall? goalToCallOnTrue = null, GoalToCall? goalToCallOnFalse = null, 
			Dictionary<string, object?>? goalToCallOnTrueParameters = null, 
			Dictionary<string, object?>? goalToCallOnFalseParameters = null)
		{
			Reasoning = reasoning;
			Namespace = @namespace;
			Name = name;
			Implementation = implementation;
			Parameters = parameters;
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
