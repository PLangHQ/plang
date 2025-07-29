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
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build<T>(GoalStep step, IBuilderError? previousBuildError = null);
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, IBuilderError? previousBuildError = null);

		Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, Type responseType, IBuilderError? previousBuildError = null);
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildWithClassDescription<T>(GoalStep step, ClassDescription classDescription, IBuilderError? previousBuildError = null);
		Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildWithClassDescription(GoalStep step, ClassDescription classDescription, IBuilderError? previousBuildError = null);
		LlmRequest GetLlmRequest(GoalStep step, Type responseType, IBuilderError? previousBuildError = null, ClassDescription? classDescription = null);
		void InitBaseBuilder(GoalStep goalStep, IPLangFileSystem fileSystem, ILlmServiceFactory llmService, ITypeHelper typeHelper,
			MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper, ILogger logger);
		void SetStep(GoalStep step);
	}
}