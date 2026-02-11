using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed partial class Step
{
    public async Task<Data> Load(PLangContext context)
    {
        var events = context.EventsFor(this);
        var before = await events.Load.Before.Run(context);
        if (!before.Success) return before;

        var actionsResult = await Actions.Load(context);
        if (!actionsResult.Success) return actionsResult;

        var after = await events.Load.After.Run(context);
        return after;
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        context.Step = this;
        context.CallStack?.RecordStep(this);

        var events = context.EventsFor(this);

        var beforeResult = await events.Before.Run(context);
        if (!beforeResult) return beforeResult;

        try
        {
            var result = await Actions.RunAsync(engine, context, cancellationToken);
            if (!result.Success) return result;

            var afterResult = await events.After.Run(context);
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
