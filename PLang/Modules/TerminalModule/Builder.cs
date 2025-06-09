using PLang.Building.Model;
using PLang.Errors.Builder;

namespace PLang.Modules.TerminalModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
		{
			AppendToAssistantCommand(@"Remove % around dataOutputVariable and errorDebugInfoOutputVariable");
			return await base.Build(goalStep, previousBuildError);

		}
	}
}

