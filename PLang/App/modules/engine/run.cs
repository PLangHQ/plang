using App.Context;
using App.Variables;

namespace App.modules.engine;

/// <summary>
/// Dispatches a single action on the target actor's context.
/// Used by run.pr's RunAction to execute one action at a time,
/// enabling PLang-level before/after action events.
/// </summary>
[Action("dispatch")]
public partial class Dispatch : IContext
{
    [IsNotNull]
    public partial Goals.Goal.Steps.Step.Actions.Action.@this Action { get; init; }

    /// <summary>
    /// Target actor to execute as. If null, executes on the current context.
    /// </summary>
    public partial Actor? Actor { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        var callingActor = Context.Actor;
        var targetActor = Actor;

        PLangContext execContext;
        if (targetActor != null && targetActor != callingActor)
        {
            if (callingActor != null && callingActor.EscalationLevel < targetActor.EscalationLevel)
                return Data.FromError(new Errors.ActionError(
                    $"Actor '{callingActor.Name}' cannot escalate to '{targetActor.Name}'",
                    "EscalationDenied", 403));

            execContext = targetActor.Context;
        }
        else
        {
            execContext = Context;
        }

        var previousActor = engine.CurrentActor;
        if (targetActor != null) engine.CurrentActor = targetActor;

        try
        {
            var result = await engine.Run(Action, execContext);
            result.Handled = true;
            return result;
        }
        finally
        {
            engine.CurrentActor = previousActor;
        }
    }
}
