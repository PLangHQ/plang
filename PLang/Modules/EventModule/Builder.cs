using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors.Builder;
using PLang.Events;
using PLang.Models;
using PLang.Runtime;
using PLang.Utils;
using System.Diagnostics;
using static PLang.Modules.DbModule.Builder;

namespace PLang.Modules.EventModule;

public class Builder : BaseBuilder
{
	private readonly IGoalParser goalParser;
	private readonly PrParser prParser;

	public Builder(IGoalParser goalParser, PrParser prParser)
	{
		this.goalParser = goalParser;
		this.prParser = prParser;
	}
	public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
	{
		SetSystem(EventBuilder.GetSystemPrompt(false));
		return await base.Build(goalStep, previousBuildError);
	}

	public async Task<(Instruction, IBuilderError?)> BuilderValidate(GoalStep step, Instruction instruction, GenericFunction gf)
	{
		var eventBinding = gf.GetParameter<EventBinding>("eventBinding");
		if (eventBinding?.GoalToCall == null) return (instruction, new BuilderError("GoalToCall is empty. You must call some goal on an event"));

		var goalToCall = eventBinding.GoalToCall;
		if (goalToCall == null)
		{
			return (instruction, new StepBuilderError("Goal name is empty", step, "GoalNotDefined", Retry: true));
		}

		if (goalToCall.Name.Contains("%"))
		{
			return (instruction, null);
		}

		var disableSystemGoals = gf.GetParameter<bool>("disableSystemGoals");
		var goals = goalParser.GetGoals();
		var systemGoals = (disableSystemGoals) ? new List<Goal>() : prParser.GetSystemGoals();

		(var goal, var error) = GoalHelper.GetGoal(step.RelativeGoalPath, step.Goal.AbsoluteAppStartupFolderPath, goalToCall, goals, systemGoals);
		if (error != null && error.StatusCode == 404) return (instruction, new BuilderError(error) { Retry = false });
		if (error != null) return (instruction, new BuilderError(error));


		goalToCall.Path = goal.RelativePrPath;
		eventBinding = eventBinding with { GoalToCall = goalToCall };

		gf = gf.SetParameter("eventBinding", eventBinding);
		instruction = instruction with { Function = gf };

		return (instruction, null);

	}
}

