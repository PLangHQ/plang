namespace app.@event.clause;

/// <summary>
/// An event that runs a clause — the program's <c>on.error</c>, <c>on.cache</c>, <c>on.timeout</c> — on the action it
/// follows. Bound when the program is read, for every actor. At each run the clause's handler is minted with that run's
/// context (its parameters read there — a cache key renders per run), and handed the action it is a clause of.
/// </summary>
public sealed class @this : global::app.@event.@this
{
    private readonly global::app.goal.step.action.@this _clause;
    private readonly global::app.goal.step.action.@this _action;
    private readonly Func<global::app.module.ICodeGenerated, global::app.goal.step.action.@this, global::app.data.@this,
        global::app.actor.context.@this, Task<global::app.data.@this>> _fire;

    /// <summary>The clause <paramref name="clause"/> of <paramref name="action"/>, run <paramref name="when"/> its moment
    /// <paramref name="name"/>.</summary>
    internal @this(string name, global::app.@event.When when, global::app.goal.step.action.@this clause,
        global::app.goal.step.action.@this action,
        Func<global::app.module.ICodeGenerated, global::app.goal.step.action.@this, global::app.data.@this,
            global::app.actor.context.@this, Task<global::app.data.@this>> fire)
        : base(action, name, when, clause.Module.App.actor.list.System)
    {
        _clause = clause;
        _action = action;
        _fire = fire;
    }

    internal override async Task<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        var (handler, error) = await _clause.Bind(context);
        if (error != null) return context.Error(error);
        return await _fire(handler!, _action, result, context);
    }
}
