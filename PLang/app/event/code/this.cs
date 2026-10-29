namespace app.@event.code;

/// <summary>
/// An event C# bound: debug's watch, the test runner's timing, coverage. When its moment comes for an item its filter
/// takes, it runs its handler; for any other it says nothing. A snapshot never keeps it: its maker binds it again.
/// </summary>
public sealed class @this : global::app.@event.@this
{
    /// <summary>The filter of an event that runs for every item.</summary>
    internal static readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> Always = (_, _) => true;

    private readonly System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this,
        System.Threading.Tasks.Task<global::app.data.@this>> _handler;
    private readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> _filter;

    internal @this(global::app.type.item.@this item, string name, global::app.@event.When when,
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        global::app.actor.@this actor, System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> filter)
        : base(item, name, when, actor)
    {
        _handler = handler;
        _filter = filter;
    }

    internal override System.Threading.Tasks.Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context, global::app.@event.@this @event)
    {
        if (!_filter(item, context)) return System.Threading.Tasks.Task.FromResult(result);
        return _handler(item, result, context);
    }
}
