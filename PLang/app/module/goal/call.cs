using app;
using app.actor.context;
using app.variable;

namespace app.module.goal;

/// <summary>
/// Calls a named goal, optionally on a different actor.
/// Parameters are injected into the target goal's context by the GoalCall resolver.
/// </summary>
[Action("call")]
public partial class Call : IContext
{
    public partial data.@this<GoalCall> GoalName { get; init; }

    /// <summary>
    /// Target actor to run the goal on. If null, runs on the current context.
    /// </summary>
    public partial data.@this<actor.@this>? Actor { get; init; }

    /// <summary>
    /// Build-time hook: drop a self-reference arg — one whose name equals the variable it
    /// references (<c>path=%path%</c>). It is redundant: the callee already reads that variable
    /// (shared scope, or reads cascade out of a fork), so passing it is the same as not passing
    /// it. Detected by a build-time string check (never at runtime); the dropped arg is announced.
    /// </summary>
    public async Task<data.@this> Build()
    {
        var goalCall = await GoalName.Value();
        if (goalCall?.Parameters == null) return Context.Ok();
        for (int i = goalCall.Parameters.Count - 1; i >= 0; i--)
        {
            var p = goalCall.Parameters[i];
            if (!string.Equals(p.Peek()?.ToString(), $"%{p.Name}%", System.StringComparison.OrdinalIgnoreCase))
                continue;
            await Context.App.Debug.Write(
                $"build: dropped redundant self-reference '{p.Name}=%{p.Name}%' in call to {goalCall.Name}");
            goalCall.Parameters.RemoveAt(i);
        }
        return Context.Ok();
    }

    public async Task<data.@this> Run()
    {
        var goalCall = (await GoalName.Value())!;
        // Stamp THIS action as the anchor so GetGoalAsync can navigate step → goal → sub-goals.
        // Using __action (source-generator self-reference) instead of the step's first action
        // means nested goal.call instances — e.g. inside error.handle's Actions chain —
        // navigate from themselves, not from the outer action that owns the step.
        if (goalCall.Action == null)
            goalCall.Action = __action;
        // No actor given (param absent OR its value is null) → run in the current
        // actor's context. Only resolve Actor when it actually holds one, so a null
        // value never tries to convert into an actor.
        var execContext = (Actor == null || await Actor.IsEmpty() ? null : await Actor.Value())?.Context ?? Context;
        return await Context.App.RunGoalAsync(goalCall, execContext);
    }
}
