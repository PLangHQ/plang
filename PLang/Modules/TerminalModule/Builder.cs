using PLang.Building.Model;

namespace PLang.Modules.TerminalModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public async Task<Instruction> Build(GoalStep goalStep)
		{
			AppendToAssistantCommand(@"Remove % around dataOutputVariable and errorDebugInfoOutputVariable");
			return await base.Build(goalStep);

		}
	}
}

