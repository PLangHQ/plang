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
    /// The actor to run the goal on, by name. If null, runs on the current context.
    /// </summary>
    public partial data.@this<global::app.type.item.choice.@this<actor.Name>>? Actor { get; init; }

    /// <summary>Safe to run beside its siblings — a fact about this call. How many run at once is
    /// the runner's decision (e.g. llm.query's tool loop).</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Parallel { get; init; }

    /// <summary>
    /// Build-time hook: drop a self-reference arg — one whose name equals the variable it
    /// references (<c>path=%path%</c>). It is redundant: the callee already reads that variable
    /// (shared scope, or reads cascade out of a fork), so passing it is the same as not passing
    /// it. Detected by a build-time string check (never at runtime); the dropped arg is announced.
    /// </summary>
    public async Task<data.@this> Build()
    {
        // The name becomes the goal's own address — one truth, a dictionary hit at run. A %variable%
        // name is only known at run and stays authored; a goal in the caller's own file (a child, or
        // the file's root) stays bare — it wins by rule and cannot be shadowed. A goal not found yet
        // may be built later in the same run, so the name is left as written.
        var caller = __action?.Step?.Goal;
        if (await Callee() is { } target
            && (await Name.Value())?.RawText is { } authored
            && !Equals(target.Path, caller?.Path) && target.Address is { } address
            && !string.Equals(address, authored, System.StringComparison.OrdinalIgnoreCase)
            && __action!["Name"] is { } name)
            __action.Property.Set(new global::app.goal.step.action.property.@this
                { Name = name.Name, Type = name.Type, Value = new global::app.type.item.text.@this(address), Properties = name.Properties });

        if (Parameter?.Peek() is not global::app.type.item.list.@this args) return Context.Ok();

        var kept = new List<data.@this>();
        foreach (var arg in args.Items(Context))
        {
            if (string.Equals(arg.Peek()?.ToString(), $"%{arg.Name}%", System.StringComparison.OrdinalIgnoreCase))
            {
                await (Context.App.Debug?.Write(
                    $"build: dropped redundant self-reference '{arg.Name}=%{arg.Name}%' in call to {Name.Peek()}") ?? Task.CompletedTask);
                continue;
            }
            kept.Add(arg);
        }
        // The argument list is the action's own property — replace it with the survivors.
        if (kept.Count != args.CountRaw && __action?["Parameter"] is { } arguments)
            __action.Property.Set(new global::app.goal.step.action.property.@this
                { Name = arguments.Name, Type = arguments.Type, Value = new global::app.type.item.list.@this(kept), Properties = arguments.Properties });
        return Context.Ok();
    }

    /// <summary>The goal this call names, selected through the goal collection as seen from the goal
    /// this call sits in — the selection <see cref="Run"/> makes. A %variable% name answers none.</summary>
    public async Task<global::app.goal.@this?> Callee()
    {
        if (Name.HasVariableReference) return null;
        var authored = (await Name.Value())?.RawText;
        return string.IsNullOrEmpty(authored) ? null : await Context.App.Goal.GetAsync(authored, __action?.Step?.Goal);
    }

    public async Task<data.@this> Run()
    {
        // The goal is selected through the goal collection as seen from the goal this call sits in.
        // A %variable% name resolves here, in the caller's context.
        var goal = await Context.App.Goal.GetAsync((await Name.Value())?.RawText ?? "", __action?.Step?.Goal);
        if (goal == null)
            return Context.Error(new global::app.error.ActionError($"Goal '{Name.Peek()}' not found.", "GoalNotFound", 404));

        // No actor given (param absent OR its value is null) → run in the current
        // actor's context. Only resolve Actor when it actually holds one, so a null
        // value never tries to convert into an actor.
        var named = Actor == null || await Actor.IsEmpty() ? null : await Actor.Value();
        var execContext = named == null ? Context : Context.App.Actor[named].Context;

        // Data just flows — each argument binds under its name as-is, no inspection, no resolve;
        // it resolves on its own door when the callee reads it. Goal-call is not a fork: the writes
        // land in whatever scope the caller's flow is in.
        // A row that carries no value is a declaration ("this goal takes a city"), not an argument —
        // it binds nothing. A valued row is "this value unless the invocation supplied one": a runner
        // that forks (a tool invocation) runs this call inside a frame born with the arguments it
        // supplies, and a supplied name wins. A call in the flow (plain, or a callback) has no such
        // frame, so its rows always bind.
        // Each argument binds as its own Data born with the CALLER's context: `place=%city%` loads
        // from the caller's memory whoever reads it, stays unresolved until read, and the shared row
        // never enters the callee's variables. The list loads on this run's own copy, never the row.
        if (Parameter != null && await Parameter.Value() is global::app.type.item.list.@this args)
            foreach (var arg in args.Items(Context))
            {
                if (arg.Peek() is not { IsNull: false }) continue;
                if (execContext.Variable.Supplies(__action, arg.Name)) continue;
                await execContext.Variable.Set(arg.Name, arg.Copy(Context));
            }

        return await goal.Run(execContext);
    }
}
