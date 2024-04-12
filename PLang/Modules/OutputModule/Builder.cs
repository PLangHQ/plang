using PLang.Building.Model;

namespace PLang.Modules.OutputModule
{
	public class Builder : BaseBuilder
	{
		public override async Task<Instruction> Build(GoalStep step)
		{

			return await base.Build(step);
			

		}
	}
}
