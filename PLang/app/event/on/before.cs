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

    private protected override async System.Threading.Tasks.ValueTask<global::app.data.@this> Run(global::app.@event.@this[] events,
        global::app.type.item.@this item, global::app.data.@this result, global::app.actor.context.@this context)
    {
        foreach (var @event in events)
        {
            if (!@event.For(item, context)) continue;
            var answer = await @event.Start(item, result, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return result;
    }
}
