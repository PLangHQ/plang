using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors.Runtime
{

	public record ProgramError : StepError
	{

		public ProgramError(
							string Message,
							GoalStep? Step = null,
							IGenericFunction? GenericFunction = null,
							IDictionary<string, object?>? ParameterValues = null,
							string Key = "ProgramError",
							int StatusCode = 400,
							Exception? Exception = null,
							string? FixSuggestion = null,
							string? HelpfulLinks = null)
		: base(Message, Step, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
			this.GenericFunction = GenericFunction;
			this.ParameterValues = ParameterValues;
		}

		public IGenericFunction? GenericFunction { get; set; }
		public IDictionary<string, object?>? ParameterValues { get; set; }
		public override string ToString()
		{
			return base.ToString();
		}
	}
}
