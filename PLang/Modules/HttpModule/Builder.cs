using PLang.Building.Model;

namespace PLang.Modules.HttpModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<Instruction> Build(GoalStep goalStep)
		{
			AppendToAssistantCommand(@"If user uses JSONPath to describe how to load variable in ReturnValue, keep the $ for the ReturnValue.VariableName, but only if he defines JSONPath.\n");
			
			return await base.Build(goalStep);
			
		}


	}
}

