using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action;

public sealed partial class @this
{
    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context);
        if (!beforeResult) return beforeResult;

        Data result;
        if (beforeResult.Handled)
        {
            // Before-event provided an override — skip action
            result = beforeResult;
        }
        else
        {
            var (action, error) = engine.Modules.GetCodeGenerated(Module, ActionName, context);
            if (error != null)
                return Data.FromError(error);

            result = await action!.ExecuteAsync(this, engine, context);
        }

        result.Context = context;

        if (this.Return != null)
        {
            foreach (var returnVar in this.Return)
            {
                result.Name = returnVar.Name;
                context.MemoryStack.Put(result);

                // Transfer disposable ownership to parent frame
                if (result.Value is IDisposable or IAsyncDisposable)
                {
                    var currentFrame = context.CallStack?.Current;
                    if (currentFrame?.Parent != null)
                        currentFrame.TransferDisposable(result.Value, currentFrame.Parent);
                }
            }
        }

        var afterResult = await lifecycle.After.Run(context);
        if (!afterResult) return afterResult;

        return result;
    }
}
