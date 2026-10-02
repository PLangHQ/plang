using app;
using app.actor.context;

namespace app.module.goal;

/// <summary>
/// Calls a named goal, optionally on a different actor. The goal is selected through the goal
/// collection as seen from the goal this call sits in; each parameter binds as a variable in the
/// context the goal runs under.
/// </summary>
[Action("call")]
public partial class Call : IContext
{
    /// <summary>The goal to call, made from its name — a bare one (a child or a goal in the caller's folder), a
    /// slash-qualified one (<c>BuildGoal/Start</c>), an app-absolute one
    /// (<c>/system/builder/EmitBuildEvent</c>), or a %variable% that holds one — selected as seen from the
    /// goal this call sits in.</summary>
    public partial data.@this<global::app.goal.@this> Name { get; init; }

    /// <summary>The parameters — one named, typed row each, bound as a variable of that name in the
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

    /// <summary>Whether the step waits for the goal to end (<c>call X, don't wait</c> is false): not waiting, the goal
    /// runs on its own and the step goes on at once; what it fails with is reported on its actor's error channel.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> Wait { get; init; }

    /// <summary>
    /// Build-time: the name becomes the goal's own address — one truth, a dictionary hit at run. A %variable%
    /// name is only known at run and stays authored; a goal in the caller's own file stays bare
    /// (<see cref="global::app.goal.@this.Reference"/>). A goal not found yet may be built later in the same
    /// run, so the name is left as written. The parameters stay as written: <c>x=%x%</c> gives the callee its
    /// own <c>%x%</c>, starting as the caller's.
    /// </summary>
    public async Task<data.@this> Build()
    {
        // at build, the goal is the one the name selects from the goal being built (the frame is the builder's)
        if (!Name.HasVariable && __action?["Name"] is { Value.RawText: { Length: > 0 } authored } name
            && await Context.App.goal.list.Find(authored, __action.Step?.Goal) is { } target
            && target.Reference(__action.Step?.Goal) is { } address
            && !string.Equals(address, authored, System.StringComparison.OrdinalIgnoreCase))
            __action.Property.Set(name.Holding(new global::app.type.item.text.@this(address)));
        return Context.Ok();
    }

    public async Task<data.@this> Start()
    {
        // The goal is the one the name selects, as seen from the goal this call sits in; a %variable% name
        // selects here, in the caller's context.
        if (await Name.Value() is not { } goal) return Name;

        // the actor named runs it; none named, this one
        if (await Wait.ToBooleanAsync())
            return await Context.App.actor.list.Use(Actor, Context, runner => Run(goal, runner.Context));

        // not waited for: it runs on its own, and a failure nothing waits for goes to its actor's error channel
        var context = Context;
        _ = Task.Run(async () =>
        {
            data.@this ran;
            try { ran = await context.App.actor.list.Use(Actor, context, runner => Run(goal, runner.Context)); }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            {
                ran = context.Error(global::app.error.Error.FromException(ex));
            }
            if (!ran.Success) await (ran.Context ?? context).Actor.Channel.Report(ran);
        });
        return Context.Ok();
    }

    private async Task<data.@this> Run(global::app.goal.@this goal, global::app.actor.context.@this execContext)
    {
        // The parameters bind in the call's own frame, in the memory the callee runs in: they are the
        // callee's for as long as it runs and gone when it returns; any other write the callee makes
        // reaches that memory as it would without the call. Each parameter is settled in the caller and binds
        // under its name.
        // A row that carries no value is a declaration ("this goal takes a city"), not a value given —
        // it binds nothing. A valued row is "this value unless the invocation supplied one": a runner
        // that forks (a tool invocation) runs this call inside a frame born with the parameters it
        // supplies, and a supplied name wins.
        // Each parameter binds as its own Data: `place=%city%` is the caller's %city% as it is now, and the
        // shared row never enters the callee's variables. The list loads on this run's own copy, never the row.
        // The parameters are named rows: the written list's, or a dict's entries when they are given as one value
        // at run (`Parameter=%asked.parameters%`) — read as what the reference names, never converted to a list.
        var bound = new List<data.@this>();
        if (Parameter != null && await (await Parameter.Follow(Context)).Value() is { } parameters)
            foreach (var parameter in parameters.Rows(Context))
            {
                if (parameter.Peek() is not { IsNull: false }) continue;
                if (execContext.Variable.Supplies(__action, parameter.Name)) continue;
                // A parameter is read where it is written, in the caller, as a set reads its value: a reference
                // (`goal=%goal%`) binds what it names now, unread; a template (`name="%first% %last%"`) renders
                // here, with the caller's variables. A reference to nothing leaves the name unset in the callee;
                // a value that can't be read (a template naming an unset variable, a variable holding a failure)
                // fails the call, in the caller.
                var settled = await parameter.Settle();
                if (settled.IsInitialized && !settled.Success) return settled;
                bound.Add(settled.IsInitialized ? settled.Copy(parameter.Name) : Context.NotFound(parameter.Name));
            }

        await using (execContext.Variable.Calls.Push(bound))
            return await goal.Start(execContext);
    }
}
