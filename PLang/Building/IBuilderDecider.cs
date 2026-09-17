using PLang.Building.Model;
using PLang.Errors;

namespace PLang.Building
{
	// A decision engine the builder asks "which one of these?" rather than "generate this".
	// Typesafe.ai is the first implementation; anything that can rank a finite set of options
	// against a step fits here. Generation (SQL, code, templates) stays on ILlmService.
	public interface IBuilderDecider
	{
		// The engine's own shape: one state, any number of questions, answered together. Everything
		// below is a caller of this. A whole goal's steps go in one request this way, which is both
		// far fewer round trips and more accurate, because each question is answered with the rest
		// of the goal visible as context.
		Task<(Dictionary<string, DeciderAnswer>? Answers, IError? Error)> Choose(string state, Dictionary<string, DeciderQuestion> questions);

		Task<(ModuleChoice? Choice, IError? Error)> ChooseModule(string stepText, Dictionary<string, string> modules);
		Task<(MethodChoice? Choice, IError? Error)> ChooseMethod(string stepText, string module, Dictionary<string, string> methods);

		// One question per parameter, all answered in a single call. Each entry is the parameter
		// name mapped to what it is and its candidate options. The answer maps the same parameter
		// names to the chosen option key and how sure the engine is.
		Task<(Dictionary<string, ParameterChoice>? Choices, IError? Error)> ChooseParameters(string stepText, string method, Dictionary<string, ParameterQuestion> parameters);
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
