using PLang.Building.Model;
using PLang.Errors.Builder;

namespace PLang.Modules.CallGoalModule;

public class Builder : BaseBuilder
{
    public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step)
    {
        AppendToAssistantCommand(@"
== Examples starts ==
call !ParseText => ParseText is goalName, no parameters
call !Gmail/Search %query%, => Gmail/Search is goalName,  %query% is key and value in parameters
call Folder/Search q=%fileName% => Folder/Search is goalName, parameter key is q, and value is %fileName%
== Examples ends ==
");
        return await base.Build(step);
    }
}