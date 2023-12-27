using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;

namespace PLang.Modules
{
    public interface IBaseBuilder
	{
		Task<Instruction> Build<T>(GoalStep step);
		Task<Instruction> Build(GoalStep step, Type responseType);
		Task<Instruction> Build(GoalStep step);
		LlmQuestion GetQuestion(GoalStep step, Type responseType);
		void InitBaseBuilder(string module, IPLangFileSystem fileSystem, ILlmService llmService, ITypeHelper typeHelper, MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper);
	}
}