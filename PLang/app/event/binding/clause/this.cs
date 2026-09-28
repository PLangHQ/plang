namespace app.@event.binding.clause;

/// <summary>
/// A binding that runs a clause — the program's <c>on.error</c>, <c>on.cache</c>, <c>on.timeout</c> — on the
/// action it follows. Bound when the program is read, for every actor. At each firing the clause's handler is
/// minted with that firing's context (its parameters read there — a cache key renders per run), and handed the
/// action it is a clause of.
/// </summary>
public sealed class @this : global::app.@event.binding.@this
{
    private readonly global::app.goal.step.action.@this _clause;
    private readonly global::app.goal.step.action.@this _action;
    private readonly Func<global::app.module.ICodeGenerated, global::app.goal.step.action.@this, global::app.data.@this,
        global::app.actor.context.@this, Task<global::app.data.@this>> _fire;

    internal @this(global::app.@event.binding.list.@this side, global::app.goal.step.action.@this clause,
        global::app.goal.step.action.@this action,
        Func<global::app.module.ICodeGenerated, global::app.goal.step.action.@this, global::app.data.@this,
            global::app.actor.context.@this, Task<global::app.data.@this>> fire)
        : base(side, clause.Module.App.actor.list.System, global::app.@event.binding.Scope.app, Always)
    {
        _clause = clause;
        _action = action;
        _fire = fire;
    }

    private protected override async Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        var (handler, error) = await _clause.Bind(context);
        if (error != null) return context.Error(error);
        return await _fire(handler!, _action, result, context);
    }
}
