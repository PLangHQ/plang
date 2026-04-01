using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.error;

/// <summary>
/// Checks a Data result for errors. Handles: ignoreError, retry (already done by ExecuteWithRetry),
/// error goal, or propagates. The step's OnError determines behavior.
/// </summary>
[Action("check", Cacheable = false)]
public partial class Check : IContext, IAction
{
    public partial Data Data { get; init; }

    public async Task<Data> Run()
    {
        if (Data == null || Data.Success) return Data ?? Engine.Memory.Data.Ok();

        // Read the user step from MemoryStack — it has the OnError
        var userStep = Context.MemoryStack.GetValue("step") as PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this;
        var onError = userStep?.OnError;
        // OnError determines behavior: ignore, retry, error goal, or propagate

        // No error handler — propagate (clear Handled so RunSteps stops)
        if (onError == null)
        {
            Data.Handled = false;
            return Data;
        }

        // Ignore — swallow the error
        if (onError.IgnoreError)
            return Engine.Memory.Data.Ok();

        // Error goal — call it, then consider the error handled
        if (onError.Goal != null)
        {
            // Stamp the GoalCall's Action so it can navigate to the user step's goal sub-goals
            onError.Goal.Action ??= userStep!.Actions.FirstOrDefault();
            var engine = Context.Engine!;
            await engine.RunGoalAsync(onError.Goal, Context);
            return Engine.Memory.Data.Ok();
        }

        // No handler matched — propagate (clear Handled so RunSteps stops)
        Data.Handled = false;
        return Data;
    }
}
