using PLang.Building.Model;
using PLang.Errors.Builder;

namespace PLang.Modules.InjectModule;

public class Builder : BaseBuilder
{
    public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep)
    {
        var setup = goalStep.RelativePrPath.ToLower().StartsWith("setup") ? "true" : "false";
        AppendToSystemCommand($@"
if user does not define if injection is global for whole app, then globalForWholeApp={setup}
");

        return await base.Build(goalStep);
    }
}