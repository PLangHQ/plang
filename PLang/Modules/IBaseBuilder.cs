using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLang.Modules
{
    public interface IBaseBuilder
	{
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build<T>(GoalStep step);
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, Type responseType, string? errorMessage = null, int errorCount = 0);
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step);
		LlmRequest GetLlmRequest(GoalStep step, Type responseType, string? errorMessage = null);
		void InitBaseBuilder(string module, IPLangFileSystem fileSystem, ILlmServiceFactory llmService, ITypeHelper typeHelper,
			MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper, ILogger logger);
	}
}