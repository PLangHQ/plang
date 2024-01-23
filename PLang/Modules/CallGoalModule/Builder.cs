using PLang.Building.Model;

namespace PLang.Modules.CallGoalModule
{
	public class Builder : BaseBuilder
    {
		public override async Task<Instruction> Build(GoalStep step)
		{

			SetSystem($@"
 Parse user command, to match the parameters needed for the RunGoal function

Variables are defined with starting and ending %

GoalName should be prefixed with !
Parameters are optional, they are key value of a variable, if user does not define key, then it is same as value.

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

