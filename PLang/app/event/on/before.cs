namespace app.@event.on;

/// <summary>
/// What starts before a moment. An event that fails stops what it is before — its error is the answer — and one that
/// answers Handled cancels it: its answer is the result, and the events after it don't start. A plain success says
/// nothing; the result passes on as is.
/// </summary>
public sealed class before : side
{
    /// <summary>A moment's before: it takes events.</summary>
    internal before(@this on) : base(on) { }

    private before() : base(null) { }

    /// <summary>The before of the shared empty moments: closed, it never holds an event.</summary>
    internal static readonly before None = new();

    internal override When When => When.before;

    /// <summary>Starts each event, in the order bound, for <paramref name="item"/>, handed <paramref name="about"/> (what
    /// the moment is about to do: a set's value, a load's .pr), else a plain success. A failure or a Handled answer stops
    /// it and is the answer; else <paramref name="about"/> passes on. Null when nothing is bound — nothing said, nothing
    /// made.</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this?> Start(global::app.type.item.@this item,
        global::app.actor.context.@this context, global::app.data.@this? about = null)
    {
        var events = Events;
        return events.Length == 0 ? default : Run(events, item, context, about);
    }

    private async System.Threading.Tasks.ValueTask<global::app.data.@this?> Run(global::app.@event.@this[] events,
        global::app.type.item.@this item, global::app.actor.context.@this context, global::app.data.@this? about)
    {
        events = await Running(events, context);
        if (events.Length == 0) return null;
        var result = about ?? context.Ok();
        foreach (var @event in events)
        {
            // an event already running in this flow is passed by: what it starts has reached its moment again
            using var run = context.Enter(@event);
            if (run == null) continue;
            var answer = await @event.Start(item, result, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return result;
    }

    /// <summary>The failure <paramref name="failed"/> handed to each event in the order bound (an error's before) that
    /// takes it — a failure raised while an event runs is offered only to its app's error events: the first answers —
    /// anything but the failure handed back (a retry's result, a recovery's, an ignore, or a new failure) — and the rest
    /// never see it. Null when none takes it. Once a before has answered a failure, it is the failure of the goal it was
    /// before: a sub app's own error events on that goal can hide the denial from its caller, never run the goal past it
    /// (a retry runs the check again, a recovery goal is checked too).</summary>
    internal async System.Threading.Tasks.ValueTask<global::app.data.@this?> Catch(global::app.type.item.@this item,
        global::app.data.@this failed, global::app.actor.context.@this context)
    {
        var events = Events;
        if (events.Length == 0) return null;
        // an action's own clauses take every failure of their action; an actor's events only those their app's raised
        var raised = on.list == null ? null : context.call.@event;
        foreach (var @event in await Running(events, context))
        {
            if (!@event.Takes(raised)) continue;
            using var run = context.Enter(@event);
            if (run == null) continue;
            var answer = await @event.Start(item, failed, context);
            if (answer != failed) return answer;
        }
        return null;
    }
}
