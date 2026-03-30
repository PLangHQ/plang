using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Merges LLM-derived fields from StepFromLlm onto Step. Delegates to Step.Merge().
/// </summary>
[Action("steps.merge")]
public partial class merge : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    [IsNotNull]
    public partial Step StepFromLlm { get; init; }

    public Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Task.FromResult(Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400)));

        Step.Merge(StepFromLlm);
        return Task.FromResult(Data.Ok(Step));
    }
}
