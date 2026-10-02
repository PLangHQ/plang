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

    /// <summary>Runs on its own (<c>call X in parallel</c>): the call answers a <c>task</c> at once and the step goes
    /// on; the task is waited for, cancelled or left alone later. A failure nobody waits for goes to its actor's error
    /// channel.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Parallel { get; init; }

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
            && (await Context.App.goal.list.Find(authored, __action.Step?.Goal)).Peek() is global::app.goal.@this target
            && target.Reference(__action.Step?.Goal) is { } address
            && !string.Equals(address, authored, System.StringComparison.OrdinalIgnoreCase))
            __action.Property.Set(name.Holding(new global::app.type.item.text.@this(address)));
        return Context.Ok();
    }

    /// <summary>Build-time: the parameters are named rows, read as <see cref="Run"/> reads them. A %variable% known
    /// only at run is the run's to read.</summary>
    public async Task<global::app.error.Error?> Validate()
    {
        if (Parameter == null) return null;
        var given = await Parameter.Follow(Context);
        if (!given.IsInitialized || !given.Success || await given.Value() is not { IsNull: false } value) return null;
        return Unnamed(value);
    }

    // Why the parameters bind nothing, or null when each is a named row: a value that is no rows at all (a module by
    // its %!…% path) or a row with no name binds nothing, so the call is refused saying the form.
    private global::app.error.Error? Unnamed(global::app.type.item.@this value)
    {
        if (value.Rows(Context) is { } rows && rows.All(row => row.Name.Length > 0)) return null;
        var written = __action?["Parameter"]?.Value?.RawText ?? value.ToString();
        return new global::app.error.Error(
            $"Parameter takes named rows: {{name: {written}}} — {written} is one value with no name, which binds nothing",
            "ParameterUnnamed", 400);
    }

    public async Task<data.@this> Start()
    {
        // The goal is the one the name selects, as seen from the goal this call sits in; a %variable% name
        // selects here, in the caller's context.
        if (await Name.Value() is not { } goal) return Name;

        // the actor named runs it; none named, this one — to its end, or in parallel as one of its tasks, in a context
        // of its own whose first frame keeps every write the task makes and reads through to the frame this call is
        // in, then this context's memory
        var parallel = await Parallel.ToBooleanAsync();
        var from = Context.call.Current;
        return await Context.App.actor.list.Use(Actor, Context, async runs => parallel
            ? Context.Ok(runs.Actor.Task.Start(goal, async token =>
            {
                using var child = Context.Child(runs.Actor, token);
                await using (child.call.Isolate(null, caller: from))
                    return await Run(goal, child);
            }))
            : await Run(goal, runs));
    }

    private async Task<data.@this> Run(global::app.goal.@this goal, global::app.actor.context.@this execContext)
    {
        // The parameters are the variables the goal's frame is born with: they are the callee's for as long as it
        // runs and gone when it returns; any other write the callee makes reaches its memory as it would without
        // the call. Each parameter is settled in the caller and binds under its name.
        // A row that carries no value is a declaration ("this goal takes a city"), not a value given —
        // it binds nothing. A valued row is "this value unless the invocation supplied one": a runner
        // that forks (a tool invocation) runs this call inside a frame born with the parameters it
        // supplies, and a supplied name wins.
        // Each parameter binds as its own Data: `place=%city%` is the caller's %city% as it is now, and the
        // shared row never enters the callee's variables. The list loads on this run's own copy, never the row.
        // The parameters are named rows: the written list's, or a dict's entries when they are given as one value
        // at run (`Parameter=%asked.parameters%`) — read as what the reference names, never converted to a list.
        var bound = new List<data.@this>();
        // a null holds no parameters: it binds nothing, as none written does
        if (Parameter != null && await (await Parameter.Follow(Context)).Value() is { IsNull: false } parameters)
        {
            if (Unnamed(parameters) is { } unnamed) return Context.Error(unnamed);
            foreach (var parameter in parameters.Rows(Context)!)
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
        }

        return await goal.Start(execContext, bound);
    }
}
