using PLang.Building.Model;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors.Builder
{
	public interface IInvalidModuleError
	{
		public string ModuleType { get; }
	}
	public record InvalidModuleStepError(string ModuleType, string Message, GoalStep Step, string? FixSuggestion = null, string? HelpfulLinks = null) :
		StepBuilderError(Message, Step, "InvalidModule", FixSuggestion: FixSuggestion, HelpfulLinks: HelpfulLinks), IInvalidModuleError
	{
	}


	public record InvalidModuleError(string ModuleType, string Message, IGenericFunction GenericFuction, string? FixSuggestion = null, string? HelpfulLinks = null) :
		InstructionBuilderError(Message, GenericFuction.Instruction.Step, GenericFuction.Instruction, "InvalidModule", FixSuggestion: FixSuggestion, HelpfulLinks: HelpfulLinks), IInvalidModuleError
	{
		
	}
}
