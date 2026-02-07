using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Utility;

namespace PLang.Runtime2.Core;

public sealed partial class Goal
{
    public static Goal FromData(GoalData data, string? prPath = null)
    {
        return GoalDataConverter.ToGoal(data, prPath: prPath);
    }

    public async Task Load(PLangContext context)
    {
        await context.Events.OnBeforeGoalLoad.Run(context, this);

        await Steps.Load(context);

        await context.Events.OnAfterGoalLoad.Run(context, this);
    }

    public async Task<Return> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        context.Goal = this;
        context.CurrentGoalName = Name;

        if (cancellationToken.IsCancellationRequested)
            return new Return { Error = GoalError.Cancelled(context) };

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
                    {
                        var errorBindings = context.Events.GetMatchingBindings(EventType.OnError, Name);
                        if (errorBindings.Count > 0)
                        {
                            var errorList = new EventList();
                            foreach (var b in errorBindings) errorList.Add(b);
                            await errorList.Run(context);
                        }
                        return stepResult;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    return new Return { Error = GoalError.Cancelled(context) };
            }

            var afterResult = await Events.After.Run(context);
            if (!afterResult) return afterResult;

            return new Return();
        }
        finally
        {
            context.CallStack?.Pop();
        }
    }
}
