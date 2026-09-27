namespace app.@event.binding.list;

/// <summary>
/// What starts after an event: every binding, whatever the others answer — what ran can't be cancelled. A binding
/// that fails makes the result its error, and the ones after it start on that. A success says nothing; the result
/// passes on as is.
/// </summary>
public sealed class after : @this
{
    /// <summary>An own event's list: it takes bindings.</summary>
    internal after() : base(open: true) { }

    private after(bool open) : base(open) { }

    /// <summary>The after of the shared empty events: closed, it never holds a binding.</summary>
    internal static readonly after None = new(open: false);

    private protected override async System.Threading.Tasks.ValueTask<global::app.data.@this> Run(binding.@this[] bindings,
        global::app.type.item.@this item, global::app.data.@this result, global::app.actor.context.@this context)
    {
        foreach (var binding in bindings)
        {
            if (!binding.For(item, context)) continue;
            var answer = await binding.Start(item, result, context);
            if (!answer.Success) result = answer;
        }
        return result;
    }
}
