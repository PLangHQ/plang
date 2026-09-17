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
	}
}
