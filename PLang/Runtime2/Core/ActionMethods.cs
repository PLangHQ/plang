using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using PLang.Runtime2.modules;

namespace PLang.Runtime2.Core;

public sealed partial class Action
{
    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        var events = context.EventsFor(this);

        var beforeResult = await events.Before.Run(context);
        if (!beforeResult) return beforeResult;

        var (handler, error) = engine.Actions.GetCodeGenerated(Module, ActionName, context);
        if (error != null)
            return Data.Fail(error);

        var result = await handler!.CodeGeneratedExecuteAsync(Parameters, engine, context);

        if (result.Value != null && this.Return != null)
        {
            foreach (var returnVar in this.Return)
                context.MemoryStack.Set(returnVar.Name, result.Value, result.Type);
        }

        var afterResult = await events.After.Run(context);
        if (!afterResult) return afterResult;

        return result;
    }
}
