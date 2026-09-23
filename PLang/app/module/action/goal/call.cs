using app;
using app.actor.context;
using app.variable;

namespace app.module.action.goal;

/// <summary>
/// Calls a named goal, optionally on a different actor. The goal is selected through the goal
/// collection as seen from the goal this call sits in; each argument binds as a variable in the
/// context the goal runs under.
/// </summary>
[Action("call")]
public partial class Call : IContext
{
    /// <summary>The goal to call — a bare name (a child or a goal in the caller's folder), a
    /// slash-qualified one (<c>BuildGoal/Start</c>), an app-absolute one
    /// (<c>/system/builder/EmitBuildEvent</c>), or a %variable% that holds one.</summary>
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>The arguments — one named, typed row each, bound as a variable of that name in the
    /// called goal.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Parameter { get; init; }

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
        if (Parameter?.Peek() is not global::app.type.item.list.@this args) return Context.Ok();

        var kept = new List<data.@this>();
        foreach (var arg in args.Items)
        {
            if (string.Equals(arg.Peek()?.ToString(), $"%{arg.Name}%", System.StringComparison.OrdinalIgnoreCase))
            {
                await (Context.App.Debug?.Write(
                    $"build: dropped redundant self-reference '{arg.Name}=%{arg.Name}%' in call to {Name.Peek()}") ?? Task.CompletedTask);
                continue;
            }
            kept.Add(arg);
        }
        // The argument list is the action's own row — rebind it with the survivors.
        if (kept.Count != args.Items.Count)
            foreach (var row in __action.Parameter)
                if (string.Equals(row.Name, "Parameter", System.StringComparison.OrdinalIgnoreCase))
                    row.SetValue(new global::app.type.item.list.@this(kept, Context));
        return Context.Ok();
    }

    public async Task<data.@this> Run()
    {
        // A %variable% name resolves here, at dispatch, in the caller's context.
        var name = (await Name.Value())?.ToString() ?? "";
        var goal = await Context.App.Goal.GetAsync(name, __action?.Step?.Goal);
        if (goal == null)
            return Context.Error(new global::app.error.ActionError($"Goal '{name}' not found.", "GoalNotFound", 404));

        // No actor given (param absent OR its value is null) → run in the current
        // actor's context. Only resolve Actor when it actually holds one, so a null
        // value never tries to convert into an actor.
        var execContext = (Actor == null || await Actor.IsEmpty() ? null : await Actor.Value())?.Context ?? Context;

        // Data just flows — each argument binds under its name as-is, no inspection, no resolve;
        // it resolves on its own door when the callee reads it. Goal-call is not a fork: the writes
        // land in whatever scope the caller's flow is in.
        if (Parameter?.Peek() is global::app.type.item.list.@this args)
            foreach (var arg in args.Items)
            {
                arg.Context = execContext;
                await execContext.Variable.Set(arg.Name, arg);
            }

        return await goal.Run(execContext);
    }
}
