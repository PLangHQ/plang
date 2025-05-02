using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors.Runtime
{
	public record ProgramError(string Message, GoalStep? Step = null, GenericFunction? GenericFunction = null, IDictionary<string, object?>? ParameterValues = null,
			string Key = "ProgramError", int StatusCode = 400,  Exception? Exception = null, string? FixSuggestion = null, string? HelpfulLinks = null)
			: StepError(Message, Step, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
	{
		public GenericFunction? GenericFunction { get; set; } = GenericFunction;
		public override string ToString()
		{
			return base.ToString(); 
		}
	}
}
