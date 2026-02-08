using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed partial class Step
{
    public async Task Load(PLangContext context)
    {
        context.PopulateLoadEvents(Events, EventType.OnBeforeStepLoad, EventType.OnAfterStepLoad);
        await Events.Before.Load.Run(context);

        await Actions.Load(context);

        await Events.After.Load.Run(context);
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        context.Step = this;
        context.CallStack?.RecordStep(Index, Text);

        var beforeResult = await Events.Before.Run(context);
        if (!beforeResult) return beforeResult;

        try
        {
            var result = await Actions.RunAsync(engine, context, cancellationToken);
            if (!result.Success) return result;

            var afterResult = await Events.After.Run(context);
            if (!afterResult) return afterResult;

            return result;
        }
        catch (Exception ex)
        {
            var error = StepError.FromException(ex, context);
            context.CallStack?.AddError(error);
            return Data.Fail(error);
        }
    }
}
