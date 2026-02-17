using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Runtime2.Engine;

public sealed partial class Action
{
    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context);
        if (!beforeResult) return beforeResult;

        Data result;
        if (beforeResult.Handled)
        {
            // Before-event provided an override — skip action handler
            result = beforeResult;
        }
        else
        {
            var (handler, error) = engine.Libraries.GetCodeGenerated(Module, ActionName, context);
            if (error != null)
                return Data.FromError(error);

            result = await handler!.CodeGeneratedExecuteAsync(Parameters, engine, context);
        }

        if (result.Value != null && this.Return != null)
        {
            foreach (var returnVar in this.Return)
                context.MemoryStack.Set(returnVar.Name, result.Value, result.Type);
        }

        var afterResult = await lifecycle.After.Run(context);
        if (!afterResult) return afterResult;

        return result;
    }
}
