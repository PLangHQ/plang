using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed partial class Goal
{
    public async Task Load(PLangContext context)
    {
        context.PopulateLoadEvents(Events, EventType.OnBeforeGoalLoad, EventType.OnAfterGoalLoad);
        await Events.Before.Load.Run(context);

        await Steps.Load(context);

        await Events.After.Load.Run(context);
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        context.Goal = this;
        context.CurrentGoalName = Name;

        if (cancellationToken.IsCancellationRequested)
            return Data.Fail(GoalError.Cancelled(context));

        var beforeResult = await Events.Before.Run(context);
        if (!beforeResult) return beforeResult;

        context.CallStack?.Push(Name, Path);

        try
        {
            for (var i = 0; i < Steps.Count; i++)
            {
                context.CurrentStepIndex = i;
                var step = Steps[i];

                var stepResult = await step.RunAsync(engine, context, cancellationToken);
                if (!stepResult)
                {
                    if (!(step.OnError?.IgnoreError ?? false))
                        return stepResult;
                }

                if (cancellationToken.IsCancellationRequested)
                    return Data.Fail(GoalError.Cancelled(context));
            }

            var afterResult = await Events.After.Run(context);
            if (!afterResult) return afterResult;

            return Data.Ok();
        }
        finally
        {
            context.CallStack?.Pop();
        }
    }
}
