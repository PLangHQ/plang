using PLang.Building.Model;

namespace PLang.Modules.CallGoalModule
{
	public class Builder : BaseBuilder
    {
		public override async Task<Instruction> Build(GoalStep step)
		{

			AppendToAssistantCommand($@"
== Examples starts ==
!ParseText then ParseText is goalName, no parameters
!Gmail/Search %query%, then Gmail/Search is goalName,  %query% is key and value in parameters
Folder/Search q=%fileName%, then key is q, and value is %fileName%
== Examples ends ==
");
			return await base.Build(step);
		}

	}
}

