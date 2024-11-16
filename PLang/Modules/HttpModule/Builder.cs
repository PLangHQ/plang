using PLang.Building.Model;
using PLang.Errors.Builder;

namespace PLang.Modules.HttpModule;

public class Builder : BaseBuilder
{
    public override async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep goalStep)
    {
        //AppendToAssistantCommand(@"If user uses JSONPath to describe how to load variable in ReturnValue, keep the $ for the ReturnValue.VariableName, but only if he defines JSONPath.\n");

        return await base.Build(goalStep);
    }
}