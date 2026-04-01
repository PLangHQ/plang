using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.engine;

/// <summary>
/// Kernel step dispatch — runs a step's actions via engine.Run().
/// </summary>
[Action("execute")]
public partial class Execute : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        var savedGoal = Context.Goal;

        // Set context goal to the step's goal so goal.call finds sub-goals
        if (Step.Goal != null) Context.Goal = Step.Goal;

        // Inject context on Events so %step.events.before% resolves
        Step.Events.Context = Context;

        Data result = Data.Ok();
        foreach (var action in Step.Actions)
        {
            result = await engine.Run(action, Context);
            if (!result.Success) break;
        }

        Context.Goal = savedGoal;
        return result;
    }
}
