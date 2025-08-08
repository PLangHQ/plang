using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Events;

namespace PLang.Modules.EventModule;
public class Builder : BaseBuilder
{
	public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
	{
		SetSystem(EventBuilder.GetSystemPrompt(false));
		return await base.Build(goalStep, previousBuildError);
	}
}

