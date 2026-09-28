namespace app.@event.binding.call;

/// <summary>
/// A binding that runs a program's call (<c>on.event</c>) when its event fires. The call reads <c>%!event%</c> —
/// the running event, with <c>!item</c> (what it fired for) and <c>!result</c> (the result so far, its value) —
/// held on the frame the event fired in for exactly as long as the call runs: gone when it returns, each parallel
/// firing (its own frame) sees its own, and a nested event's call its own. No frame (a C#-only firing): no
/// <c>%!event%</c>.
/// </summary>
public sealed class @this : global::app.@event.binding.@this
{
    private readonly global::app.@event.@this _event;
    private readonly global::app.goal.step.action.@this _call;

    internal @this(global::app.@event.binding.list.@this side, global::app.@event.@this @event,
        global::app.goal.step.action.@this call, global::app.actor.@this actor, global::app.@event.binding.Scope scope)
        : base(side, actor, scope, Always)
    {
        _event = @event;
        _call = call;
    }

    private protected override async Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        // a property holds a value, never a Data — the result's value; a failed result is %!error%
        var running = new global::app.data.@this("!event", _event, context: context);
        running.Properties.Set("item", item);
        running.Properties.Set("result", result.Peek());

        var frame = context.CallStack.Current;
        if (frame == null) return await _call.Start(context);
        var outer = frame.Event;
        frame.Event = running;
        try { return await _call.Start(context); }
        finally { frame.Event = outer; }
    }
}
