namespace app.@event.on;

/// <summary>
/// What starts after a moment: every event, whatever the others answer — what ran can't be cancelled. An event that
/// fails makes the result its error, and the ones after it start on that. A success says nothing; the result passes on
/// as is.
/// </summary>
public sealed class after : side
{
    /// <summary>A moment's after: it takes events.</summary>
    internal after(@this on) : base(on) { }

    private after() : base(null) { }

    /// <summary>The after of the shared empty moments: closed, it never holds an event.</summary>
    internal static readonly after None = new();

    internal override When When => When.after;

    /// <summary>Starts each event, in the order bound, for <paramref name="item"/> on <paramref name="result"/> as it
    /// stands; answers the result. With nothing bound it answers <paramref name="result"/> as is.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        foreach (var @event in await Running(context))
        {
            var answer = await @event.Start(item, result, context);
            if (!answer.Success) result = answer;
        }
        return result;
    }
}
