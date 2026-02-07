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
        var handler = engine.Actions.Get(Class, Method);
        if (handler == null)
            return new Return { Error = ActionError.NotFound($"Action '{Class}.{Method}'", context) };

        if (handler is not ICodeGenerated codeGenerated)
            return new Return { Error = new ActionError($"Handler '{Class}.{Method}' does not implement ICodeGenerated", context, "HandlerError", 500) { ActionClass = Class, ActionMethod = Method } };

        var result = await codeGenerated.CodeGeneratedExecuteAsync(Parameters, engine, context);

        if (result.Success && this.Return != null)
        {
            foreach (var returnVar in this.Return)
                context.MemoryStack.Set(returnVar.Name, result.Value);
        }

        return result;
    }
}
