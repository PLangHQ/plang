using app;
using app.actor.context;

namespace app.module.goal;

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
    /// Build-time: the name becomes the goal's own address — one truth, a dictionary hit at run. A %variable%
    /// name is only known at run and stays authored; a goal in the caller's own file stays bare
    /// (<see cref="global::app.goal.@this.Reference"/>). A goal not found yet may be built later in the same
    /// run, so the name is left as written. The arguments stay as written: <c>x=%x%</c> gives the callee its
    /// own <c>%x%</c>, starting as the caller's.
    /// </summary>
    public async Task<data.@this> Build()
    {
        if (await Callee() is { } target && target.Reference(__action?.Step?.Goal) is { } address
            && !string.Equals(address, (await Name.Value())?.RawText, System.StringComparison.OrdinalIgnoreCase)
            && __action!["Name"] is { } name)
            __action.Property.Set(name.Holding(new global::app.type.item.text.@this(address)));
        return Context.Ok();
    }

    /// <summary>The goal this call names, selected through the goal collection as seen from the goal
    /// this call sits in — the selection <see cref="Start"/> makes. A %variable% name answers none.</summary>
    public async Task<global::app.goal.@this?> Callee()
    {
        if (Name.HasVariable) return null;
        var authored = (await Name.Value())?.RawText;
        return string.IsNullOrEmpty(authored) ? null : await Context.App.goal.list.Find(authored, __action?.Step?.Goal);
    }

    public async Task<data.@this> Start()
    {
        // The goal is selected through the goal collection as seen from the goal this call sits in.
        // A %variable% name resolves here, in the caller's context.
        var goal = await Context.App.goal.list.Find((await Name.Value())?.RawText ?? "", __action?.Step?.Goal);
        if (goal == null)
            return Context.Error(new global::app.error.ActionError($"Goal '{Name.Peek()}' not found.", "GoalNotFound", 404));

        // the actor named runs it; none named, this one
        return await Context.App.actor.list.Use(Actor, Context, runner => Run(goal, runner.Context));
    }

    private async Task<data.@this> Run(global::app.goal.@this goal, global::app.actor.context.@this execContext)
    {
        // The arguments bind in the call's own frame, in the memory the callee runs in: they are the
        // callee's for as long as it runs and gone when it returns; any other write the callee makes
        // reaches that memory as it would without the call. Data just flows — each argument binds under
        // its name as-is, unresolved until the callee reads it.
        // A row that carries no value is a declaration ("this goal takes a city"), not an argument —
        // it binds nothing. A valued row is "this value unless the invocation supplied one": a runner
        // that forks (a tool invocation) runs this call inside a frame born with the arguments it
        // supplies, and a supplied name wins.
        // Each argument binds as its own Data: `place=%city%` is the caller's %city% as it is now, and the
        // shared row never enters the callee's variables. The list loads on this run's own copy, never the row.
        var bound = new List<data.@this>();
        if (Parameter != null && await Parameter.Value() is global::app.type.item.list.@this args)
            foreach (var arg in args.Items(Context))
            {
                if (arg.Peek() is not { IsNull: false }) continue;
                if (execContext.Variable.Supplies(__action, arg.Name)) continue;
                // A reference argument (`goal=%goal%`) binds what it names now, unread — a frame entry that
                // named itself would cycle when read.
                bound.Add((await arg.Follow(Context)).Copy(arg.Name));
            }

        await using (execContext.Variable.Calls.Push(bound))
            return await goal.Start(execContext);
    }
}
