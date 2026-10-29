namespace app.@event.code;

/// <summary>
/// An event C# bound: debug's watch, the test runner's timing, coverage. It runs its handler when its moment comes. A
/// snapshot never keeps it: its maker binds it again.
/// </summary>
public sealed class @this : global::app.@event.@this
{
    private readonly System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this,
        System.Threading.Tasks.Task<global::app.data.@this>> _handler;

    internal @this(global::app.@event.on.side side,
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        global::app.actor.@this actor, global::app.@event.Scope scope,
        System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> filter)
        : base(side, actor, scope, filter)
        => _handler = handler;

    internal override System.Threading.Tasks.Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context, global::app.@event.@this @event)
        => _handler(item, result, context);
}
