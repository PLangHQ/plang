using Newtonsoft.Json;

namespace PLang.Services.CompilerService
{
	public class ConditionImplementationResponse : ImplementationResponse
	{
		public ConditionImplementationResponse() { }
		public ConditionImplementationResponse(string name, string? implementation = null, string[]? @using = null, string[]? assemblies = null, string? goalToCallOnTrue = null, string? goalToCallOnFalse = null)
		{
			Name = name;
			Implementation = implementation;
			Using = @using;
			Assemblies = assemblies;
			GoalToCallOnTrue = goalToCallOnTrue;
			GoalToCallOnFalse = goalToCallOnFalse;
		}

		public string? GoalToCallOnTrue { get; set; } = null;
		public string? GoalToCallOnFalse { get; set; } = null;
	}
}
