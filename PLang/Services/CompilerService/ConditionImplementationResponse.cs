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
			List<string>? assemblies = null, GoalToCallInfo? goalToCallOnTrue = null, GoalToCallInfo? goalToCallOnFalse = null)
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
		}

		public GoalToCallInfo? GoalToCallOnTrue { get; set; } = null;
		public GoalToCallInfo? GoalToCallOnFalse { get; set; } = null;		
	}
}
