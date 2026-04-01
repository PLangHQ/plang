using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.engine;

/// <summary>
/// Kernel step dispatch — runs a step's actions via engine.Run().
/// Wraps execution in a timeout if step.Timeout is set.
/// </summary>
[Action("execute")]
public partial class Execute : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;

        if (Step.Timeout is > 0)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(Context.CancellationToken);
            timeoutCts.CancelAfter(Step.Timeout.Value);

            // Push timeout token onto context so all sub-calls respect it
            Context.PushCancellation(timeoutCts);
            try
            {
                return await ExecuteActions(engine);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested)
            {
                var result = Data.FromError(new ServiceError(
                    $"Step timed out after {Step.Timeout}ms: {Step.Text}",
                    "Timeout", 408));
                result.Handled = true;
                return result;
            }
            finally
            {
                Context.PopCancellation();
            }
        }

        return await ExecuteActions(engine);
    }

    private async Task<Data> ExecuteActions(Engine.@this engine)
    {
        Data result = Data.Ok();
        foreach (var action in Step.Actions)
        {
            Context.CancellationToken.ThrowIfCancellationRequested();
            result = await engine.Run(action, Context);
            if (!result.Success) break;
        }

        result.Handled = true;
        return result;
    }
}
