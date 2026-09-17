using PLang.Building.Model;
using PLang.Errors;

namespace PLang.Building
{
	// A decision engine the builder asks "which one of these?" rather than "generate this".
	// Typesafe.ai is the first implementation; anything that can rank a finite set of options
	// against a step fits here. Generation (SQL, code, templates) stays on ILlmService.
	public interface IBuilderDecider
	{
		Task<(ModuleChoice? Choice, IError? Error)> ChooseModule(string stepText, Dictionary<string, string> modules);
		Task<(MethodChoice? Choice, IError? Error)> ChooseMethod(string stepText, string module, Dictionary<string, string> methods);
	}

	public static class BuilderDecider
	{
		// Below this the decider's pick is not trusted and the step falls back to the llm.
		public const double ConfidenceThreshold = 0.7;

		public static bool IsOn()
		{
			return (AppContext.GetData("decider") as string) != "off";
		}
	}
}
