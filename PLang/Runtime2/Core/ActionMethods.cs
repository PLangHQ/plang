using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using PLang.Runtime2.actions;

namespace PLang.Runtime2.Core;

public sealed partial class Action
{
    public async Task Load(PLangContext context)
    {
        context.PopulateLoadEvents(Events, EventType.OnBeforeActionLoad, EventType.OnAfterActionLoad);
        await Events.Before.Load.Run(context);
        await Events.After.Load.Run(context);
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        var (handler, error) = engine.Actions.GetCodeGenerated(Class, Method, context);
        if (error != null)
            return Data.Fail(error);

        var result = await handler!.CodeGeneratedExecuteAsync(Parameters, engine, context);

        if (result.Value != null && this.Return != null)
        {
            foreach (var returnVar in this.Return)
                context.MemoryStack.Set(returnVar.Name, result.Value, result.Type);
        }

        return result;
    }
}
