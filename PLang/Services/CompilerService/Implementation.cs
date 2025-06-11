
using PLang.Models;
using static PLang.Modules.BaseBuilder;

namespace PLang.Services.CompilerService
{
	public class Implementation
	{
		public Implementation(string @namespace, string name, string code, string[]? @using, List<Parameter> parameters,
			List<ReturnValue>? returnValues, GoalToCallInfo? goalToCallOnTrue, GoalToCallInfo? goalToCallOnFalse,
			Dictionary<string, object?>? goalToCallOnTrueParameters = null,
			Dictionary<string, object?>? goalToCallOnFalseParameters = null, List<string>? servicesAssembly = null)
		{
			Namespace = @namespace;
			Name = name;
			Code = code;
			Using = @using;
			Parameters = parameters;
			ReturnValues = returnValues;
			GoalToCallOnTrue = goalToCallOnTrue;
			GoalToCallOnFalse = goalToCallOnFalse;
			GoalToCallOnTrueParameters = goalToCallOnTrueParameters;
			GoalToCallOnFalseParameters = goalToCallOnFalseParameters;
			ServicesAssembly = servicesAssembly;
		}

		public string Namespace { get; }
		public string Name { get; private set; }
		public string Code { get; private set; }
		public string[]? Using { get; private set; }
		public List<Parameter> Parameters { get; private set; }
		public List<ReturnValue>? ReturnValues { get; private set; }
		public GoalToCallInfo? GoalToCallOnTrue { get; private set; }
		public GoalToCallInfo? GoalToCallOnFalse { get; private set; }
		public Dictionary<string, object?>? GoalToCallOnTrueParameters { get; set; } = null;
		public Dictionary<string, object?>? GoalToCallOnFalseParameters { get; set; } = null;
		public List<string>? ServicesAssembly { get; }
	}
}
