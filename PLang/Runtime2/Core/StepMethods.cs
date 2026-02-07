using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;

namespace PLang.Runtime2.Core;

public sealed partial class Step
{
    public async Task Load(PLangContext context)
    {
        await context.Events.OnBeforeStepLoad.Run(context, this, Goal?.Name);

        await Actions.Load(context);

        await context.Events.OnAfterStepLoad.Run(context, this, Goal?.Name);
    }

    public async Task<Return> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
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
            return new Return { Error = error };
        }
    }
}
