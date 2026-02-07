using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Modules;

namespace PLang.Runtime2.Core;

public sealed partial class Action
{
    public async Task Load(PLangContext context)
    {
        await context.Events.OnBeforeActionLoad.Run(context, this);
        await context.Events.OnAfterActionLoad.Run(context, this);
    }

    public async Task<Return> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        var (handler, error) = engine.Actions.GetCodeGenerated(Class, Method, context);
        if (error != null)
            return new Return { Error = error };

        var result = await handler!.CodeGeneratedExecuteAsync(Parameters, engine, context);

        if (result.Value != null && this.Return != null)
        {
            foreach (var returnVar in this.Return)
                context.MemoryStack.Set(returnVar.Name, result.Value);
        }

        return result;
    }
}
