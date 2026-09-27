namespace app.@event.binding;

/// <summary>
/// What is bound on an event: the handler started when the event fires, whose it is (the actor that bound it,
/// or the app), and which items it fires for. Removing it is its own verb.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    /// <summary>The filter of a binding that fires for every item.</summary>
    internal static readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> Always = (_, _) => true;

    private readonly list.@this _list;
    private readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> _handler;
    private readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> _filter;

    internal @this(list.@this list,
        System.Func<global::app.type.item.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        global::app.actor.@this actor, Scope scope,
        System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> filter)
    {
        _list = list;
        _handler = handler;
        _filter = filter;
        Actor = actor;
        Scope = scope;
    }

    /// <summary>The actor that bound it.</summary>
    public global::app.actor.@this Actor { get; }

    /// <summary>Whose it is: the actor's, or the app's.</summary>
    public Scope Scope { get; }

    /// <summary>Whether it fires for <paramref name="item"/> while <paramref name="context"/>'s actor runs: its
    /// scope takes the actor, and its filter the item.</summary>
    internal bool For(global::app.type.item.@this item, global::app.actor.context.@this context)
        => (Scope == Scope.app || ReferenceEquals(context.Actor, Actor)) && _filter(item, context);

    /// <summary>Starts the handler for the item the event fired for, in the asker's context.</summary>
    internal System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.type.item.@this item, global::app.actor.context.@this context)
        => _handler(item, context);

    /// <summary>Takes it off its event: it fires no more.</summary>
    public void Remove() => _list.Remove(this);
}
